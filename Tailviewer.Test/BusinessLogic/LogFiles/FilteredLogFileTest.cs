﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using Tailviewer.BusinessLogic;
using Tailviewer.BusinessLogic.Filters;
using Tailviewer.BusinessLogic.LogFiles;

namespace Tailviewer.Test.BusinessLogic.LogFiles
{
	[TestFixture]
	public sealed class FilteredLogFileTest
	{
		[SetUp]
		public void SetUp()
		{
			_taskScheduler = new ManualTaskScheduler();
			_entries = new List<LogLine>();
			_logFile = new Mock<ILogFile>();
			_logFile.Setup(x => x.GetSection(It.IsAny<LogFileSection>(), It.IsAny<LogLine[]>()))
			        .Callback(
				        (LogFileSection section, LogLine[] entries) =>
				        _entries.CopyTo((int) section.Index, entries, 0, section.Count));
			_logFile.Setup(x => x.GetLine(It.IsAny<int>())).Returns((int index) => _entries[index]);
			_logFile.Setup(x => x.Count).Returns(() => _entries.Count);
			_logFile.Setup(x => x.EndOfSourceReached).Returns(true);

			_sections = new List<LogFileSection>();
			_listener = new Mock<ILogFileListener>();
			_listener.Setup(x => x.OnLogFileModified(It.IsAny<ILogFile>(), It.IsAny<LogFileSection>()))
			         .Callback((ILogFile l, LogFileSection s) => _sections.Add(s));
		}

		private Mock<ILogFile> _logFile;
		private List<LogLine> _entries;
		private List<LogFileSection> _sections;
		private Mock<ILogFileListener> _listener;
		private ManualTaskScheduler _taskScheduler;

		[Test]
		public void TestEndOfSourceReached()
		{
			using (var file = new FilteredLogFile(_taskScheduler, TimeSpan.Zero, _logFile.Object, Filter.Create(null, true, LevelFlags.Debug)))
			{
				_logFile.Verify(x => x.EndOfSourceReached, Times.Never);
				var unused = file.EndOfSourceReached;
				_logFile.Verify(x => x.EndOfSourceReached, Times.Once);
			}
		}

		[Test]
		[Description("Verifies that the filtered log file correctly listens to a reset event")]
		public void TestClear()
		{
			using (var file = new FilteredLogFile(_taskScheduler, TimeSpan.Zero, _logFile.Object, Filter.Create(null, true, LevelFlags.Debug)))
			{
				_entries.Add(new LogLine(0, 0, "DEBUG: This is a test", LevelFlags.Debug));
				_entries.Add(new LogLine(1, 0, "DEBUG: Yikes", LevelFlags.None));
				file.OnLogFileModified(_logFile.Object, new LogFileSection(0, 2));

				_taskScheduler.RunOnce();

				file.EndOfSourceReached.Should().BeTrue();
				file.Count.Should().Be(2);

				_entries.Clear();
				file.OnLogFileModified(_logFile.Object, LogFileSection.Reset);

				_taskScheduler.RunOnce();
				file.EndOfSourceReached.Should().BeTrue();
				file.Count.Should().Be(0);
			}
		}

		[Test]
		public void TestEmptyLogFile()
		{
			using (var file = new FilteredLogFile(_taskScheduler, TimeSpan.Zero, _logFile.Object, Filter.Create("Test", true, LevelFlags.All)))
			{
				_taskScheduler.RunOnce();

				file.EndOfSourceReached.Should().BeTrue();
				file.Count.Should().Be(0);
			}
		}

		[Test]
		public void TestEntryLevelNone()
		{
			using (var file = new FilteredLogFile(_taskScheduler, TimeSpan.Zero, _logFile.Object, Filter.Create("ello", true, LevelFlags.All)))
			{
				_entries.Add(new LogLine(0, "Hello world!", LevelFlags.None));
				file.OnLogFileModified(_logFile.Object, new LogFileSection(0, 1));

				_taskScheduler.RunOnce();

				file.EndOfSourceReached.Should().BeTrue();
				file.Count.Should().Be(1);
				file.GetSection(new LogFileSection(0, 1))
				    .Should().Equal(new[]
					    {
						    new LogLine(0, "Hello world!", LevelFlags.None)
					    });
			}
		}

		[Test]
		public void TestInvalidate1()
		{
			using (var file = new FilteredLogFile(_taskScheduler, TimeSpan.Zero, _logFile.Object, Filter.Create(null, true, LevelFlags.Info)))
			{
				_taskScheduler.RunOnce();
				file.EndOfSourceReached.Should().BeTrue();

				_entries.AddRange(new[]
					{
						new LogLine(0, 0, "A", LevelFlags.Info),
						new LogLine(1, 1, "B", LevelFlags.Info),
						new LogLine(2, 2, "C", LevelFlags.Info),
						new LogLine(3, 3, "D", LevelFlags.Info)
					});

				file.OnLogFileModified(_logFile.Object, new LogFileSection(0, 4));
				file.OnLogFileModified(_logFile.Object, new LogFileSection(2, 2, true));

				_taskScheduler.RunOnce();

				file.EndOfSourceReached.Should().BeTrue();
				file.Count.Should().Be(2, "because we've invalidated the last 2 out of 4 lines");
			}
		}

		[Test]
		public void TestInvalidate2()
		{
			using (var file = new FilteredLogFile(_taskScheduler, TimeSpan.Zero, _logFile.Object, Filter.Create(null, true, LevelFlags.Info)))
			{
				file.AddListener(_listener.Object, TimeSpan.Zero, 1);

				_taskScheduler.RunOnce();
				file.EndOfSourceReached.Should().BeTrue();

				_entries.AddRange(new[]
					{
						new LogLine(0, 0, "A", LevelFlags.Info),
						new LogLine(1, 1, "B", LevelFlags.Info),
						new LogLine(2, 2, "C", LevelFlags.Info),
						new LogLine(3, 3, "D", LevelFlags.Info)
					});
				file.OnLogFileModified(_logFile.Object, new LogFileSection(0, 4));

				_taskScheduler.RunOnce();
				file.EndOfSourceReached.Should().BeTrue();
				file.Count.Should().Be(4);

				file.OnLogFileModified(_logFile.Object, new LogFileSection(2, 2, true));

				_taskScheduler.RunOnce();
				file.EndOfSourceReached.Should().BeTrue();
				file.Count.Should().Be(2);

				_sections.Should().Equal(new[]
					{
						LogFileSection.Reset,
						new LogFileSection(0, 1),
						new LogFileSection(1, 1),
						new LogFileSection(2, 1),
						new LogFileSection(3, 1),
						new LogFileSection(2, 2, true)
					});
			}
		}

		[Test]
		[Description(
			"Verifies that the FilteredLogFile won't get stuck in an endless loop when an Invalidate() follows a multiline log entry"
			)]
		public void TestInvalidate3()
		{
			using (var file = new FilteredLogFile(_taskScheduler, TimeSpan.Zero, _logFile.Object, Filter.Create(null, true, LevelFlags.Info)))
			{
				file.AddListener(_listener.Object, TimeSpan.Zero, 1);

				_taskScheduler.RunOnce();
				file.EndOfSourceReached.Should().BeTrue();

				_entries.AddRange(new[]
					{
						new LogLine(0, 0, "A", LevelFlags.Info),
						new LogLine(1, 0, "B", LevelFlags.Info),
						new LogLine(2, 0, "C", LevelFlags.Info),
						new LogLine(3, 0, "D", LevelFlags.Info)
					});
				file.OnLogFileModified(_logFile.Object, new LogFileSection(0, 4));

				_taskScheduler.RunOnce();
				file.EndOfSourceReached.Should().BeTrue();

				file.OnLogFileModified(_logFile.Object, new LogFileSection(2, 2, true));

				_taskScheduler.RunOnce();
				file.EndOfSourceReached.Should().BeTrue("Because the filtered log file should be finished");
				file.Count.Should().Be(2);

				_sections.Should().Equal(new[]
					{
						LogFileSection.Reset,
						new LogFileSection(0, 1),
						new LogFileSection(1, 1),
						new LogFileSection(2, 1),
						new LogFileSection(3, 1),
						new LogFileSection(2, 2, true)
					});
			}
		}

		[Test]
		[Description(
			"Verifies that listeners are notified eventually, even when the # of filtered entries is less than the minimum batch size"
			)]
		public void TestListener()
		{
			_entries.Add(new LogLine(0, 0, "DEBUG: This is a test", LevelFlags.Debug));
			_entries.Add(new LogLine(1, 0, "Yikes", LevelFlags.None));
			using (var file = new FilteredLogFile(_taskScheduler, TimeSpan.Zero, _logFile.Object, Filter.Create("yikes", true, LevelFlags.All)))
			{
				var sections = new List<LogFileSection>();
				var listener = new Mock<ILogFileListener>();

				listener.Setup(x => x.OnLogFileModified(It.IsAny<ILogFile>(), It.IsAny<LogFileSection>()))
				        .Callback((ILogFile l, LogFileSection s) => sections.Add(s));
				// We deliberately set the batchSize to be greater than the amount of entries that will be matched.
				// If the FilteredLogFile is implemented correctly, then it will continously notifiy the listener until
				// the maximum wait time is elapsed.
				const int batchSize = 10;
				file.AddListener(listener.Object, TimeSpan.FromMilliseconds(100), batchSize);
				file.OnLogFileModified(_logFile.Object, new LogFileSection(0, 2));

				_taskScheduler.RunOnce();
				file.EndOfSourceReached.Should().BeTrue();

				file.Count.Should().Be(2);
				sections.Should().Equal(new[]
					{
						LogFileSection.Reset,
						new LogFileSection(0, 2)
					});
			}
		}

		[Test]
		[Description("Verifies that all lines belonging to an entry pass the filter, even though only one line passes it")]
		public void TestMultiLineLogEntry1()
		{
			using (var file = new FilteredLogFile(_taskScheduler, TimeSpan.Zero, _logFile.Object, Filter.Create("Test", true, LevelFlags.All)))
			{
				_entries.Add(new LogLine(0, 0, "DEBUG: This is a test", LevelFlags.Debug));
				_entries.Add(new LogLine(1, 0, "Yikes", LevelFlags.None));
				file.OnLogFileModified(_logFile.Object, new LogFileSection(0, 2));

				_taskScheduler.RunOnce();
				file.EndOfSourceReached.Should().BeTrue();

				file.Count.Should().Be(2);
				file.GetSection(new LogFileSection(0, 2))
				    .Should().Equal(new[]
					    {
						    new LogLine(0, 0, "DEBUG: This is a test", LevelFlags.Debug),
						    new LogLine(1, 0, "Yikes", LevelFlags.None)
					    });
			}
		}

		[Test]
		[Description(
			"Verifies that all lines belonging to an entry pass the filter, even though only the second line passes it")]
		public void TestMultiLineLogEntry2()
		{
			using (var file = new FilteredLogFile(_taskScheduler, TimeSpan.Zero, _logFile.Object, Filter.Create("yikes", true, LevelFlags.All)))
			{
				_entries.Add(new LogLine(0, 0, "DEBUG: This is a test", LevelFlags.Debug));
				_entries.Add(new LogLine(1, 0, "Yikes", LevelFlags.None));
				file.OnLogFileModified(_logFile.Object, new LogFileSection(0, 2));

				_taskScheduler.RunOnce();
				file.EndOfSourceReached.Should().BeTrue();

				file.Count.Should().Be(2);
				file.GetSection(new LogFileSection(0, 2))
				    .Should().Equal(new[]
					    {
						    new LogLine(0, 0, "DEBUG: This is a test", LevelFlags.Debug),
						    new LogLine(1, 0, "Yikes", LevelFlags.None)
					    });
			}
		}

		[Test]
		[Description("Verifies that the filtered log file repeatedly calls the listener when the source has been fully read")]
		public void TestWait()
		{
			using (var file = new FilteredLogFile(_taskScheduler, TimeSpan.Zero, _logFile.Object, Filter.Create(null, true, LevelFlags.Debug)))
			{
				var sections = new List<LogFileSection>();
				var listener = new Mock<ILogFileListener>();
				listener.Setup(x => x.OnLogFileModified(It.IsAny<ILogFile>(), It.IsAny<LogFileSection>()))
				        .Callback((ILogFile logFile, LogFileSection section) => sections.Add(section));
				file.AddListener(listener.Object, TimeSpan.FromMilliseconds(100), 3);

				_entries.Add(new LogLine(0, 0, "DEBUG: This is a test", LevelFlags.Debug));
				_entries.Add(new LogLine(1, 0, "DEBUG: Yikes", LevelFlags.None));
				file.OnLogFileModified(_logFile.Object, new LogFileSection(0, 2));

				_taskScheduler.RunOnce();
				file.EndOfSourceReached.Should().BeTrue();
				sections.Should().Equal(new object[]
					{
						LogFileSection.Reset,
						new LogFileSection(new LogLineIndex(0), 2)
					});
			}
		}

		[Test]
		[Description("Verifies that filtered log entries present the correct index from the view of the filtered file")]
		public void TestGetSection1()
		{
			using (var file = new FilteredLogFile(_taskScheduler, TimeSpan.Zero, _logFile.Object, Filter.Create("yikes", true, LevelFlags.All)))
			{
				_entries.Add(new LogLine(0, 0, "DEBUG: This is a test", LevelFlags.Debug));
				_entries.Add(new LogLine(1, 1, "Yikes", LevelFlags.None));
				file.OnLogFileModified(_logFile.Object, new LogFileSection(0, 2));

				_taskScheduler.RunOnce();
				file.EndOfSourceReached.Should().BeTrue();

				var section = file.GetSection(new LogFileSection(0, 1));
				section.Should().NotBeNull();
				section.Length.Should().Be(1);
				section[0].LineIndex.Should().Be(0, "because the filtered log file only represents a file with one line, thus the only entry should have an index of 0, not 1, which is the original index");
				section[0].Message.Should().Be("Yikes");
			}
		}

		[Test]
		[Description("Verifies that filtered log entries present the correct index from the view of the filtered file")]
		public void TestGetLine1()
		{
			using (var file = new FilteredLogFile(_taskScheduler, TimeSpan.Zero, _logFile.Object, Filter.Create("yikes", true, LevelFlags.All)))
			{
				_entries.Add(new LogLine(0, 0, "DEBUG: This is a test", LevelFlags.Debug));
				_entries.Add(new LogLine(1, 1, "Yikes", LevelFlags.None));
				file.OnLogFileModified(_logFile.Object, new LogFileSection(0, 2));

				_taskScheduler.RunOnce();
				file.EndOfSourceReached.Should().BeTrue();

				var line = file.GetLine(0);
				line.LineIndex.Should().Be(0, "because the filtered log file only represents a file with one line, thus the only entry should have an index of 0, not 1, which is the original index");
				line.Message.Should().Be("Yikes");
			}
		}
	}
}