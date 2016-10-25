﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using Tailviewer.BusinessLogic;
using Tailviewer.BusinessLogic.LogFiles;
using Tailviewer.Ui.Controls.LogView;

namespace Tailviewer.Test.Ui.Controls
{
	[TestFixture]
	public sealed class LogEntryListViewTest
	{
		private LogEntryListView _control;
		private Mock<ILogFile> _logFile;
		private List<LogLine> _lines;
		private List<ILogFileListener> _listeners;

		[SetUp]
		[STAThread]
		public void SetUp()
		{
			_control = new LogEntryListView
				{
					Width = 1024,
					Height = 768
				};
			var availableSize = new Size(1024, 768);
			_control.Measure(availableSize);
			_control.Arrange(new Rect(new Point(), availableSize));
			DispatcherExtensions.ExecuteAllEvents();

			_lines = new List<LogLine>();
			_listeners = new List<ILogFileListener>();

			_logFile = new Mock<ILogFile>();
			_logFile.Setup(x => x.Count).Returns(() => _lines.Count);
			_logFile.Setup(x => x.GetSection(It.IsAny<LogFileSection>(), It.IsAny<LogLine[]>()))
			        .Callback((LogFileSection section, LogLine[] dest) =>
			                  _lines.CopyTo((int) section.Index, dest, 0, section.Count));
			_logFile.Setup(x => x.GetLine(It.IsAny<int>())).Returns((int index) =>
			                                                        _lines[index]);
			_logFile.Setup(x => x.AddListener(It.IsAny<ILogFileListener>(), It.IsAny<TimeSpan>(), It.IsAny<int>()))
			        .Callback((ILogFileListener listener, TimeSpan maximumTimeout, int maximumLines) =>
				        {
					        _listeners.Add(listener);
					        listener.OnLogFileModified(_logFile.Object,
					                                   new LogFileSection(0, _lines.Count));
				        });
		}

		[Test]
		[STAThread]
		public void TestCtor()
		{
			_control.LogFile.Should().BeNull();
			_control.FollowTail.Should().BeFalse();
		}

		[Test]
		[STAThread]
		[Description("Verifies that an empty log file can be represented")]
		public void TestSetLogFile1()
		{
			new Action(() => _control.LogFile = _logFile.Object).ShouldNotThrow();
			_control.LogFile.Should().BeSameAs(_logFile.Object);

			DispatcherExtensions.ExecuteAllEvents();

			_control.VerticalScrollBar.Minimum.Should().Be(0, "Because a scrollviewer should always start at 0");
			_control.VerticalScrollBar.Maximum.Should().Be(0, "Because the log file is empty and thus no scrolling shall happen");
			_control.VerticalScrollBar.Value.Should().Be(0, "Because the log file is empty and thus no scrolling shall happen");
			_control.VerticalScrollBar.ViewportSize.Should().Be(768, "Because the viewport shall be as big as the control");
		}

		[Test]
		[STAThread]
		[Description("Verfies that a log file with one line can be represented")]
		public void TestSetLogFile2()
		{
			_lines.Add(new LogLine(0, 0, "Foobar", LevelFlags.Debug));

			new Action(() => _control.LogFile = _logFile.Object).ShouldNotThrow();

			_control.VisibleTextLines.Count.Should().Be(1, "Because the log file contains one log line");
			_control.VisibleTextLines[0].LogLine.Should().Be(_lines[0]);

			DispatcherExtensions.ExecuteAllEvents();

			_control.VerticalScrollBar.Minimum.Should().Be(0, "Because a scrollviewer should always start at 0");
			_control.VerticalScrollBar.Maximum.Should().Be(0, "Because the single line that is being displayed is less than the total of 48 that can be, hence no scrolling may be allowed");
			_control.VerticalScrollBar.Value.Should().Be(0, "Because there is less content than can be displayed and thus no scrolling is necessary");
			_control.VerticalScrollBar.ViewportSize.Should().Be(768, "Because the viewport shall be as big as the control");
		}

		[Test]
		[STAThread]
		[Description("Verfies that a log file with as many lines as the viewport can hold can be represented")]
		public void TestSetLogFile3()
		{
			for (int i = 0; i < 46; ++i)
			{
				_lines.Add(new LogLine(0, 0, "Foobar", LevelFlags.Debug));
			}

			new Action(() => _control.LogFile = _logFile.Object).ShouldNotThrow();

			_control.VisibleTextLines.Count.Should().Be(46, "Because the view can display 46 lines and we've added as many");
			for (int i = 0; i < 46; ++i)
			{
				_control.VisibleTextLines[i].LogLine.Should().Be(_lines[i]);
			}

			DispatcherExtensions.ExecuteAllEvents();

			_control.VerticalScrollBar.Minimum.Should().Be(0, "Because a scrollviewer should always start at 0");
			_control.VerticalScrollBar.Maximum.Should().Be(0, "Because we've added a total of 46 lines, which the view can display, and thus no scrolling should be necessary");
			_control.VerticalScrollBar.Value.Should().Be(0, "Because we've added a total of 46 lines, which the view can display, and thus no scrolling should be necessary");
			_control.VerticalScrollBar.ViewportSize.Should().Be(768, "Because the viewport shall be as big as the control minus the horizontal scrollbar");
		}

		[Test]
		[STAThread]
		[Description("Verfies that a log file with as one line more than the viewport can hold can be represented")]
		public void TestSetLogFile4()
		{
			const int lineCount = 53;
			for (int i = 0; i < lineCount; ++i)
			{
				_lines.Add(new LogLine(0, 0, "Foobar", LevelFlags.Debug));
			}

			new Action(() => _control.LogFile = _logFile.Object).ShouldNotThrow();

			// It takes some "time" until the control has done its layouting
			DispatcherExtensions.ExecuteAllEvents();

			_control.VisibleTextLines.Count.Should().Be(52, "Because the view can display 48 of the 49 lines that we've added");
			for (int i = 0; i < 52; ++i)
			{
				_control.VisibleTextLines[i].LogLine.Should().Be(_lines[i]);
			}

			_control.VerticalScrollBar.Minimum.Should().Be(0, "Because a scrollviewer should always start at 0");
			_control.VerticalScrollBar.Maximum.Should().Be(30, "Because we've added a total of 53 lines, of which the view can display 52 partially, and thus 30 pixels are missing in height");
			_control.VerticalScrollBar.Value.Should().Be(0);
			_control.VerticalScrollBar.ViewportSize.Should().Be(768, "Because the viewport shall be as big as the control");
		}

		[Test]
		[STAThread]
		[Description("Verfies that when a log file is set, its maximum number of characters for all lines is queried and used to calculate the maximum value of the horizontal scrollbar")]
		public void TestSetLogFile5()
		{
			bool called = false;
			_logFile.Setup(x => x.MaxCharactersPerLine).Returns(() =>
				{
					called = true;
					return 221;
				});
			new Action(() => _control.LogFile = _logFile.Object).ShouldNotThrow();

			_control.HorizontalScrollBar.Visibility.Should().Be(Visibility.Visible, "Because a scroll bar is necessary to view all contents of the log file");
			_control.HorizontalScrollBar.Maximum.Should().BeGreaterThan(0, "Because the log file claims that its greatest line contains more characters than can currently be represented by the control, hence a scrollbar is necessary");
			called.Should().BeTrue("Because the control should've queried the log file for its MaxCharactersPerLine property");
		}

		[Test]
		[STAThread]
		[Description("Verifies that the view synchronizes itself with the log file when the latter was modified after being attached")]
		public void TestLogFileAdd1()
		{
			_control.LogFile = _logFile.Object;
			DispatcherExtensions.ExecuteAllEvents();

			for (int i = 0; i < 1000; ++i)
			{
				_lines.Add(new LogLine(i, i, "Foobar", LevelFlags.Info));
			}
			_listeners[0].OnLogFileModified(_logFile.Object, new LogFileSection(0, _lines.Count));

			_control.VisibleTextLines.Count.Should().Be(0, "Because the view may not have synchronized itself with the log file");
			_control.PendingModificationsCount.Should().BeGreaterOrEqualTo(1, "Because this log file modification should have been tracked by the control");

			Thread.Sleep((int) (2*LogEntryListView.MaximumRefreshInterval.TotalMilliseconds));
			DispatcherExtensions.ExecuteAllEvents();

			_control.VisibleTextLines.Count.Should().Be(52, "Because the view must have synchronized itself and display the maximum of 52 lines");
		}

		[Test]
		[STAThread]
		[Description("Verifies that the ListView is capable of handling exceptions thrown by GetSection() that indicate that the log file has shrunk")]
		public void TestGetSectionThrows()
		{
			_control.LogFile = _logFile.Object;

			for (int i = 0; i < 1000; ++i)
			{
				_lines.Add(new LogLine(i, i, "Foobar", LevelFlags.Info));
			}
			_listeners[0].OnLogFileModified(_logFile.Object, new LogFileSection(0, _lines.Count));
		}

		[Test]
		[STAThread]
		[Description("Verifies that if the most recent log line becomes even partially obstructed, then the view is moved to make it fully visible when FollowTail is enabled")]
		public void TestFollowTail1()
		{
			_control.LogFile = _logFile.Object;
			DispatcherExtensions.ExecuteAllEvents();

			for (int i = 0; i < 52; ++i)
			{
				_lines.Add(new LogLine(i, i, "Foobar", LevelFlags.Info));
			}
			_listeners[0].OnLogFileModified(_logFile.Object, new LogFileSection(0, _lines.Count));

			Thread.Sleep((int)(2 * LogEntryListView.MaximumRefreshInterval.TotalMilliseconds));
			DispatcherExtensions.ExecuteAllEvents();

			_control.VerticalScrollBar.Value.Should().Be(0);
			_control.VisibleTextLines.Count.Should().Be(52);

			_control.FollowTail = true;
			_control.VerticalScrollBar.Maximum.Should().Be(15, "Because the view is missing 15 pixels to fully display the last row");
			_control.VerticalScrollBar.Value.Should().Be(15, "Because the vertical scrollbar should've moved in order to bring the last line *fully* into view");
		}

		[Test]
		[STAThread]
		[Description("Verifies that if FollowTail is enabled and lines are added to the log file, then the view scrolls to the bottom")]
		public void TestFollowTail2()
		{
			_control.LogFile = _logFile.Object;
			DispatcherExtensions.ExecuteAllEvents();

			for (int i = 0; i < 51; ++i)
			{
				_lines.Add(new LogLine(i, i, "Foobar", LevelFlags.Info));
			}
			_listeners[0].OnLogFileModified(_logFile.Object, new LogFileSection(0, _lines.Count));
			Thread.Sleep((int)(2 * LogEntryListView.MaximumRefreshInterval.TotalMilliseconds));
			DispatcherExtensions.ExecuteAllEvents();


			_control.FollowTail = true;
			_lines.Add(new LogLine(51, 51, "Foobar", LevelFlags.Info));
			_listeners[0].OnLogFileModified(_logFile.Object, new LogFileSection(0, _lines.Count));
			Thread.Sleep((int)(2 * LogEntryListView.MaximumRefreshInterval.TotalMilliseconds));
			DispatcherExtensions.ExecuteAllEvents();

			_control.VerticalScrollBar.Maximum.Should().Be(15, "Because the view is missing 15 pixels to fully display the last row");
			_control.VerticalScrollBar.Value.Should().Be(15, "Because the vertical scrollbar should've moved in order to bring the last line *fully* into view");
		}

		[Test]
		[STAThread]
		[Description("Verifies that if the code in OnTimer throws an exception, then the exception is caught and another update is scheduled")]
		public void TestOnTimerException()
		{
			_control.LogFile = _logFile.Object;
			_control.PendingModificationsCount.Should().Be(1, "Because the control should've queried an update due to attaching to the log file");

			bool exceptionThrown = false;
			_logFile.Setup(x => x.Count).Callback(() =>
				{
					exceptionThrown = true;
					throw new SystemException();
				});

			new Action(() => _control.OnTimer(null, null)).ShouldNotThrow("Because any and all exceptions must be handled inside this callback");
			_control.PendingModificationsCount.Should().Be(1, "Because another update should've been scheduled as this one wasn't fully completed");
			exceptionThrown.Should().BeTrue("Because the control should've queried the ILogFile.Count property during its update");
		}

		[Ignore("Not finished yet")]
		[Test]
		[STAThread]
		[Description("Verifies a mouse left down selects the item under it")]
		public void TestSelect1()
		{
			_lines.Add(new LogLine(0, 0, "Foobar", LevelFlags.Info));
			_lines.Add(new LogLine(1, 1, "Foobar", LevelFlags.Info));
			_control.LogFile = _logFile.Object;
			_control.SelectedIndices.Should().BeEmpty();

			_control.SelectedIndices.Should().Equal(new[]
				{
					new LogLineIndex(1)
				});
		}

		[Test]
		[STAThread]
		[Description("Verifies that only selected lines are copied to the clipboard")]
		public void TestCopyToClipboard1()
		{
			_lines.Add(new LogLine(0, 0, "Foobar", LevelFlags.Info));
			_lines.Add(new LogLine(1, 1, "Clondyke bar", LevelFlags.Info));
			_control.LogFile = _logFile.Object;
			_control.Select(1);
			_control.CopySelectedLinesToClipboard();

			Clipboard.GetText().Should().Be("Clondyke bar");
		}

		[Test]
		[STAThread]
		[Description("Verifies that multiple lines can be copied to the clipboard")]
		public void TestCopyToClipboard2()
		{
			_lines.Add(new LogLine(0, 0, "Foobar", LevelFlags.Info));
			_lines.Add(new LogLine(1, 1, "Clondyke bar", LevelFlags.Info));
			_control.LogFile = _logFile.Object;
			_control.Select(1, 0);
			_control.CopySelectedLinesToClipboard();

			Clipboard.GetText().Should().Be("Foobar\r\nClondyke bar");
		}

		[Test]
		[STAThread]
		[Description("Verifies that when the mouse wheel is used to scroll to the last line, then FollowTail is automatically enabled")]
		public void TestMouseWheelDown1()
		{
			for (int i = 0; i < 51; ++i)
			{
				_lines.Add(new LogLine(0, 0, "Foobar", LevelFlags.Info));
			}
			_control.LogFile = _logFile.Object;

			_control.VerticalScrollBar.Value.Should().BeLessThan(_control.VerticalScrollBar.Maximum);
			_control.FollowTail.Should().BeFalse();

			_control.TextCanvasOnMouseWheelDown();
			_control.VerticalScrollBar.Value.Should().Be(_control.VerticalScrollBar.Maximum);
			_control.FollowTail.Should().BeTrue("because scrolling down to the last line shall automatically enable follow tail");
		}

		[Test]
		[STAThread]
		[Description("Verifies that when the mouse wheel is used to scroll up from the last line, then FollowTail is automatically disabled")]
		public void TestMouseWheelUp1()
		{
			for (int i = 0; i < 51; ++i)
			{
				_lines.Add(new LogLine(0, 0, "Foobar", LevelFlags.Info));
			}
			_control.LogFile = _logFile.Object;

			_control.TextCanvasOnMouseWheelDown();
			_control.VerticalScrollBar.Value.Should().Be(_control.VerticalScrollBar.Maximum);
			_control.FollowTail.Should().BeTrue();

			_control.TextCanvasOnMouseWheelUp();
			_control.VerticalScrollBar.Value.Should().BeLessThan(_control.VerticalScrollBar.Maximum);
			_control.FollowTail.Should().BeFalse("because scrolling up shall automatically disable follow tail");
		}

		[Test]
		[STAThread]
		[Description("Verifies that when control+end is pressed, then the last line is both brought into view and selected")]
		public void TestControlEnd()
		{
			for (int i = 0; i < 200; ++i)
			{
				_lines.Add(new LogLine(0, 0, "Foobar", LevelFlags.Info));
			}
			_control.LogFile = _logFile.Object;

			_control.SelectedIndices.Should().BeEmpty();
			_control.FollowTail.Should().BeFalse();

			_control.PartTextCanvas.OnMoveEnd();
			_control.SelectedIndices.Should().Equal(new object[] {new LogLineIndex(199)});
			_control.PartTextCanvas.CurrentlyVisibleSection.Should().Be(new LogFileSection(150, 50));
			_control.FollowTail.Should().BeTrue("because scrolling down to the last line shall automatically enable follow tail");
		}

		[Test]
		[STAThread]
		[Description("Verifies that when control+start is pressed, then the first line is both brought into view and selected")]
		public void TestControlStart()
		{
			for (int i = 0; i < 200; ++i)
			{
				_lines.Add(new LogLine(0, 0, "Foobar", LevelFlags.Info));
			}
			_control.LogFile = _logFile.Object;

			_control.PartTextCanvas.OnMoveEnd();
			_control.SelectedIndices.Should().Equal(new object[] { new LogLineIndex(199) });
			_control.PartTextCanvas.CurrentlyVisibleSection.Should().Be(new LogFileSection(150, 50));
			_control.FollowTail.Should().BeTrue();

			_control.PartTextCanvas.OnMoveStart();
			_control.SelectedIndices.Should().Equal(new object[] { new LogLineIndex(0) });
			_control.PartTextCanvas.CurrentlyVisibleSection.Should().Be(new LogFileSection(0, 51));
			_control.FollowTail.Should().BeFalse();
		}
	}
}