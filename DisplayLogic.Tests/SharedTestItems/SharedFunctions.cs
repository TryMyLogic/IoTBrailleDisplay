using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.InMemory;
using Xunit.Abstractions;

namespace DisplayLogic.Tests.SharedTestItems
{
    internal class SharedFunctions
    {
        /// <summary>
        /// Creates a logger that writes to the xUnit test output.
        /// </summary>
        /// <typeparam name="T">The type to associate with the logger.</typeparam>
        /// <param name="output">The xUnit test output helper.</param>
        /// <returns>A tuple containing the logger and its factory for disposal.</returns>
        public static (ILogger<T> Logger, ILoggerFactory LoggerFactory) CreateTestLogger<T>(ITestOutputHelper output)
        {
            Logger serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.TestOutput(output)
                .CreateLogger();

            ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                _ = builder.AddSerilog(serilogLogger, dispose: true);
            });

            ILogger<T> logger = loggerFactory.CreateLogger<T>();
            return (logger, loggerFactory);
        }

        /// <summary>
        /// Creates a logger that writes to a new in-memory sink for capturing logs in tests.
        /// A new sink is created per call to ensure test isolation.
        /// </summary>
        /// <typeparam name="T">The type to associate with the logger.</typeparam>
        /// <returns>A tuple containing the logger, in-memory sink, and factory for disposal.</returns>
        public static (ILogger<T> Logger, InMemorySink MemorySink, ILoggerFactory LoggerFactory) CreateMemorySinkLogger<T>()
        {
            InMemorySink memorySink = new();

            Logger serilogMemoryLogger = new LoggerConfiguration()
               .MinimumLevel.Verbose()
               .WriteTo.Sink(memorySink)
               .CreateLogger();

            ILoggerFactory memoryLoggerFactory = LoggerFactory.Create(builder =>
            {
                _ = builder.AddSerilog(serilogMemoryLogger, dispose: true);
            });

            ILogger<T> logger = memoryLoggerFactory.CreateLogger<T>();
            return (logger, memorySink, memoryLoggerFactory);
        }

        /// <summary>
        /// Asserts that the in-memory sink contains exactly one log event with the specified level and message.
        /// </summary>
        /// <param name="memorySink">The in-memory sink containing logged events.</param>
        /// <param name="expectedLevel">The expected log level.</param>
        /// <param name="expectedMessage">The expected message (partial match).</param>
        public static void AssertSingleLogEvent(InMemorySink? memorySink, LogEventLevel expectedLevel, string expectedMessage)
        {
            if (memorySink == null)
            {
                Assert.Fail("Memory sink is null.");
            }

            List<LogEvent> memoryLog = [.. memorySink.LogEvents]; // [.. ] is a simplified ToList
            if (memoryLog.Count == 0)
            {
                Assert.Fail("No log events found.");
            }
            if (memoryLog.Count > 1)
            {
                Assert.Fail($"Expected exactly one log event, but found {memoryLog.Count}.");
            }

            LogEvent logEvent = memoryLog[0];
            Assert.Equal(expectedLevel, logEvent.Level);
            Assert.Contains(expectedMessage, logEvent.RenderMessage());
        }

        /// <summary>
        /// Asserts that the in-memory sink contains log events with the specified level and message.
        /// </summary>
        /// <param name="memorySink">The in-memory sink containing logged events.</param>
        /// <param name="expectedLevel">The expected log level.</param>
        /// <param name="expectedMessage">The expected message (partial match).</param>
        /// <param name="debugLogger">Optional logger to output captured log events for debugging.</param>
        /// <param name="expectedMatchCount">Optional number of expected matching log events. If null, checks for at least one match.</param>
        public static void AssertLogEventContainsMessage(InMemorySink? memorySink, LogEventLevel expectedLevel, string expectedMessage, Microsoft.Extensions.Logging.ILogger? debugLogger = null, int? expectedMatchCount = null)
        {
            if (memorySink == null)
            {
                Assert.Fail("Memory sink is null.");
            }

            if (debugLogger != null)
            {
                debugLogger.LogInformation("Logging test information");
                foreach (LogEvent logEvent in memorySink.LogEvents)
                {
                    debugLogger.LogInformation("[Captured:{Level}] {Message}", logEvent.Level, logEvent.RenderMessage());
                }
            }

            if (!memorySink.LogEvents.Any())
            {
                Assert.Fail("No log events found.");
            }

            List<LogEvent> matchingLogEvents = [.. memorySink.LogEvents.Where(logEvent => { return logEvent.Level == expectedLevel && logEvent.RenderMessage().Contains(expectedMessage); })];

            if (expectedMatchCount.HasValue)
            {
                Assert.Equal(expectedMatchCount.Value, matchingLogEvents.Count);
            }
            else
            {
                Assert.NotEmpty(matchingLogEvents);
            }
        }
    }
}
