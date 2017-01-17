using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;

namespace ConsoleApplication9
{
    class Program
    {
        static void Main(string[] args)
        {
            // This is the name of the event source.   
            // Given just the name of the eventSource you can get the GUID for the evenSource by calling this API.  
            // From a ETW perspective, the GUID is the 'true name' of the EventSource.  
            var providerGuid2 = Guid.Parse("{E13C0D23-CCBC-4E12-931B-D9CC2EEE27E4}");

            // Today you have to be Admin to turn on ETW events (anyone can write ETW events).   
            if (!(TraceEventSession.IsElevated() ?? false))
            {
                Console.WriteLine(
                    "To turn on ETW events you need to be Administrator, please run from an Admin process.");
            }

            // As mentioned below, sessions can outlive the process that created them.  Thus you need a way of 
            // naming the session so that you can 'reconnect' to it from another process.   This is what the name
            // is for.  It can be anything, but it should be descriptive and unique.   If you expect mulitple versions
            // of your program to run simultaneously, you need to generate unique names (e.g. add a process ID suffix) 
            Console.WriteLine("Creating a 'My Session' session");
            var sessionName = "My Session";
            using (var session = new TraceEventSession(sessionName, null))
            // the null second parameter means 'real time session'
            {
                // Note that sessions create a OS object (a session) that lives beyond the lifetime of the process
                // that created it (like Filles), thus you have to be more careful about always cleaning them up. 
                // An importanty way you can do this is to set the 'StopOnDispose' property which will cause the session to 
                // stop (and thus the OS object will die) when the TraceEventSession dies.   Because we used a 'using'
                // statement, this means that any exception in the code below will clean up the OS object.   
                session.StopOnDispose = true;

                // By default, if you hit Ctrl-C your .NET objects may not be disposed, so force it to.  It is OK if dispose is called twice.
                Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e) { session.Dispose(); };

                // prepare to read from the session, connect the ETWTraceEventSource to the session
                using (var source = new ETWTraceEventSource(sessionName, TraceEventSourceType.Session))
                {
                    // To demonstrate non-trivial event manipuation, we calculate the time delta between 'MyFirstEvent and 'MySecondEvent'
                    // These variables are used in this calculation 
                    int lastMyEventID = int.MinValue; // an illegal value to start with.  
                    double lastMyEventMSec = 0;

                    // Hook up the parser that knows about EventSources
                    var callParser = new ETWClrProfilerTraceEventParser(source);
                    callParser.AddCallbackForEvents(delegate(TraceEvent eventdata)
                    {
                        using (System.IO.StreamWriter file = new System.IO.StreamWriter(@"C:\etwdata.txt", true))
                        {
                            file.WriteLine(eventdata.EventName + eventdata);
                        }
                    });


                    //parser2.All += delegate (TraceEvent data)
                    //{
                    //    using (System.IO.StreamWriter file = new System.IO.StreamWriter(@"C:\etwdata.txt", true))
                    //    {
                    //        file.WriteLine(data.EventName);
                    //    }
                    //    Console.WriteLine("Event : " + data.EventName);
                    //};


                    // Enable my provider, you can call many of these on the same session to get other events.  
                    session.EnableProvider(ETWClrProfilerTraceEventParser.ProviderGuid, TraceEventLevel.Always, (ulong)ETWClrProfilerTraceEventParser.Keywords.Call);

                    Console.WriteLine("Staring Listing for events");
                    // go into a loop processing events can calling the callbacks.  Because this is live data (not from a file)
                    // processing never completes by itself, but only because someone called 'source.Close()'.  
                    source.Process();
                    Console.WriteLine();
                    Console.WriteLine("Stopping the collection of events.");
                }
            }
        }
    }
}
