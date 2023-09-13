# The HPET Emulator Project
*An approach to High Precision Event Timers in .NET*

---

## Introduction

This project was born out of the need to create a form of precise, accurate and monotonic timer
that schedules the execution of code, specifically for multimedia applications, which typically
require execution intervals of 10 or less milliseconds.

*TLDR*: There are no out-of-the-box .NET APIs (that I know if) that provide a reliable timer mechanism
with sub-millisecond precision.

## The Wishlist

In trying to find a reasonable solution, I began by accepting the fact that I might have been
setting myself up for failure -- the limitations of non-realime OSs are well-known.
Still, I kept optimistic as I could not believe that it was possible that, with such modern and
powerful systems, we are simply not capable of running pure userland code that is time-critical in
a CPU-efficient manner. I wanted a timer that would ideally be:

1. **Precise**: I would like to have sub-millisecond precision  in my timer. For example, if I
needed to process 75 images per second in real-time (assuming I have the computing power), I want
to be able to set the timer's interval to ```1000 / 75 = 13.3333``` milliseconds -- not "close",
not 13, and not 14.

1. **Accurate**: I don't want the time measurement between intervals to drift. I want to be able
to sum up the discrete firing intervals and get to the real amount of time that has elapsed.

1. **Monotonic**: I don't want my code to run at "jittery" intervals. For example, if I set the
timer interval to 20 millseconds, I don't want my event intervals to be 10, 30, 15, and 25
milliseconds -- which do average out to 20 milliseconds, but simply means I am unable to handle
time-critical code correctly. 

1. **Reliable**: I don't want to setup a timer that does not always fire at the required interval.
For example, if I were to set the interval to 50 milliseconds, I don't want to run the risk of
getting 10 events fired after 500 milliseconds because the scheduler of the OS decided to handle
my code later.

1. **Efficient**: I want a timer that does not waste CPU cycles constatly checking for elapsed
intervals. I also want to allow for context switching (thread yielding) when the wait time
are substantial, but have the code ready to be run on my thread without relying (too much) on the scheduler.

### What I tried (and failed at)

* Most modern systems come with a device called **HPET**. This stands for High Precision Event Timer
and sounded like the perfect solution to implementing reliable timers with sub-millisecond precision.
Unofortunately, I could not find any documentation on how to access the HPET with API calls.
I hit a dead end here.

* Busy Loop inside a Thread: While ```Stopwatch.GetTimestamp()``` provides high precision, high
resolution time measurements independent of system date settings, checking if a time
**Interval** has elapsed inside a tight loop will use up the entire thread slice.
We don't want to be consuming CPU cycles just to know when it's time to run our code that needs
to do the actual work. It also prevents the OS and our application to do actual work and
wastes a ton of power in the form of heat.

* Using the ```System.Threading.Timer``` which executes callback method on a thread pool thread,
and because it is interrupt based, solves the high CPU usage, but on its own, suffers from precision
issues. Typically, you will see that the minimum practical interval is between 15 and 25
millisceonds (depending on hardware and OS configuration), and the intervals are highly irregular
because, since Windows is not a real-time operating system, the scheduler will decide when the
interrupt is handled and the thread is "woken up". This is ideal, in most scenarios because it
saves battery life and balances out the different concurrent workloads. Plus,
the majority of applications don't **need** sub-millisecond precision for intervals.

* Using the ```System.Threading.Timer``` together with a call to ```timeBeginPeriod``` Win32 API
allows the said timer to increase its resolution, but in terms of accuracy, it will be
limited by system's configuration as reported by the ```timeGetDevCaps``` Win32 API. It still
uses thread pool threads which may or may not be avialable immediately and delay execution.
Furthermore, additional attention must be paid as in older versions of Windows, the
```timeBeginPeriod``` call has system-wide effects, making timer interrupts more frequent,
increasing power consumption, and potentially braking running application that potentially
rely on the typical 16 millisecond resultuion. ```timeBeginPeriod``` will set the resolution
to the lowest number provided, regardless of the order in which it was called. It is not
a last-in setting. In new versions of Windows, this has been improved and the resolution will
not be applied at the system-wide level, but also may or may not be limited to the process that
called it. All ```timeBeginPeriod``` calls must be matched by a ```timeEndPeriod``` call.

* Using any kind of .NET provided [Timer](https://learn.microsoft.com/en-us/dotnet/standard/threading/timers):
Whether user actions are executed in a single thread (like the WinForms UI thread), a WPF
Dispatcher thread, or a thead pool thread, the intervals still don't provide practical sub-millisecond
precision. Another dead end.

* Handling the [CompositionTarget.Rendering](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.compositiontarget.rendering?view=windowsdesktop-7.0)
event and checking the amount of time that has elapsed turned out to also be a failure. First, this is
timer would've been limited to WPF applications, and turns out the framerate at which the WPF updates
its surface is not regular. Instead, it is done on-demand which is a very efficient way of updating
the UI, but it is simply not suitable for executing code at monotonic, reliable,
precise and accurate time intervals.

* Using the supposedly *obsolete* [timeSetEvent](https://learn.microsoft.com/en-us/previous-versions/dd757634(v=vs.85))
Win32 API call: Microsoft practically begs users NOT to use this API and claims it is obsolete,
meaning, no longer in use. While abusing this call with high resolution settings results in
power consumption inefficiencies, the solutions that Microsoft recommends to use as alternatives,
completely prevent us programmers from increasing the interrupt rate in our applications -- the whole
point in order to increase precision and reduce CPU usage. As evidenced pretty much in every forum
I consulted, this API call is still very much in use. Spoiler alert: this is part of the solution.

## The Solution

Solving the puzzle took me a few days (see commit history), but in the end, I was very satified with
the outcome. Let's look at some fundamental building blocks and their usage.

### The ```DelayProvider```

The ```DelayProvider``` is the most important piece of this codebase. It provides static methods
that allow the user to block until a set amount of time elapses. It provides both, synchronous and
asynchronous versions so that it can be easily used. Here's how it works:

1. Call the ```timeSetEvent``` API as a one-shot event, maximum resolution, and 1 millisecond delay.
1. The prior step makes an interrupt occur as quickly as supported by the system and calls a method.
1. The said timer callback method then checks for either of 2 conditions:
   * If the interval is "close enough" to elapsed, do some ```SpinWait.SpinOnce()``` preventing
     context switching or if context switching is about to occur, just keep a ticght loop.
   * If the interval can be waited out with more timer one-shot events, just repeat the cycle at step 1.

When I say "close enough" what I mean is that I go ahead and ask for the system's capabilities with
```timeGetDevCaps```, and do spinning based on the maximum (smallest number) supported resolution.
This is how the ```DelayPrecision``` option determines how long spin-wait loops can last for.
```DelayPrecision.Default``` does not do any sort of tight spin-waits and that's why it jitters more but
has neglegible CPU usage.
In the other hand, ```DelayPrecision.Maximum``` tight spin-waits for **twice** the maximum supported
timer resolution and that is why you'll see an increase in CPU usage but highly reduced "jittering"
between intervals.

#### Usage Examples

```cs

// Setting a specific delay precision.
DelayProvider.Delay(TimeSpan.FromMilliseconds(10), DelayPrecision.Medium, CancellationToken.None);

// Skipping all options (delay precision is Default)
DelayProvider.Delay(TimeSpan.FromMilliseconds(10);

// Asynchronous version
await DelayProvider.DelayAsync(TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);

// Using the TimeSpan extension methods
TimeSpan.FromMilliseconds(10).Delay();

// Asynchronous extension methods
await TimeSpan.FromMilliseconds(10).DelayAsync();

```

### The ```IPrecisionLoop```

I wanted to implement an interval scheduler in 3 main forms:
1. A background thread (```PrecisionThread```)
1. A timer with a Tick event (```PrecisionTimer```)
1. A long-running Task with async/await support (```PrecisionTask```)

All implementations define the same looping logic that automatically and precisely
keep a loop state that adjusts the intervals dynamically based on how long the user's (your)
code took to execute, how much the ```Stopwatch``` has drifted from actual elapsed time, and
how much on average have the intervals drifted from the requested target interval.

Once you instantiate any of these classes, simply call their ```Start()``` method.
You can stop the execution with via 2 different mechanisms.
  1. While handling the event, simply set the ```PrecisionCycleEventArgs.IsStopRequested``` to true.
  1. If outside the event handler code (different thread) simply call the ```Dispose``` method and
optionally, await the ```WaitForExitAsync()``` method.

Note: calling the ```Dispose()``` method is non-blocking and only **signals** the precision thread
to stop. There is no guarantee that no more cycles will be executed as it may have been called just after
a new cycle has begun executing. Calling ```Dispose()``` more than once has no effect.

#### Usage Example

```cs
        // Create a high precision event thread
        var scheduler = new PrecisionThread((e) =>
        {
            Console.WriteLine("Cycle Executed!");

            if (e.NaturalElapsed > TimeSpan.FromSeconds(5))
                e.IsStopRequested = true;
        },
        interval: TimeSpan.FromMilliseconds(10),
        DelayPrecision.High);

        scheduler.Start();
        // pressing a key anytime before the 5 seconds elapsed
        // will block at WaitForExitAsync
        Console.ReadKey(true);
        await scheduler.WaitForExitAsync();
        Console.WriteLine("Cycles are finished!");
```

### The ```PrecisionThreadBase```

If you want customized, encapsulated logic implemented in a high precision thread, I recommend
you inherit from the ```PrecisionThreadBase``` class and override the various methods avaiable
for implementation.


That's all. Have fun!
