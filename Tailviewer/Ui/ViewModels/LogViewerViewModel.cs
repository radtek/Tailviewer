﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Metrolib;
using Tailviewer.BusinessLogic;
using Tailviewer.BusinessLogic.DataSources;
using Tailviewer.BusinessLogic.Filters;
using Tailviewer.BusinessLogic.LogFiles;
using Tailviewer.BusinessLogic.Searches;

namespace Tailviewer.Ui.ViewModels
{
	public sealed class LogViewerViewModel
		: INotifyPropertyChanged
		  , ILogFileListener
	{
		private readonly IDataSourceViewModel _dataSource;
		private readonly IDispatcher _dispatcher;
		private readonly TimeSpan _maximumWaitTime;
		private readonly List<KeyValuePair<ILogFile, LogFileSection>> _pendingSections;
		private ILogFile _logFile;
		private int _logEntryCount;
		private string _noEntriesExplanation;
		private string _noEntriesSubtext;
		private int _totalLogEntryCount;
		private ILogFileSearch _search;

		public LogViewerViewModel(IDataSourceViewModel dataSource, IDispatcher dispatcher, TimeSpan maximumWaitTime)
		{
			if (dataSource == null) throw new ArgumentNullException("dataSource");
			if (dispatcher == null) throw new ArgumentNullException("dispatcher");

			_maximumWaitTime = maximumWaitTime;
			_dataSource = dataSource;

			_dispatcher = dispatcher;

			_pendingSections = new List<KeyValuePair<ILogFile, LogFileSection>>();

			LogFile = _dataSource.DataSource.FilteredLogFile;
			LogFile.AddListener(this, _maximumWaitTime, 1000);
			Search = _dataSource.DataSource.Search;

			UpdateCounts();
		}

		public LogViewerViewModel(IDataSourceViewModel dataSource, IDispatcher dispatcher)
			: this(dataSource, dispatcher, TimeSpan.FromMilliseconds(10))
		{
		}

		public override string ToString()
		{
			return _logFile.ToString();
		}

		public ILogFile LogFile
		{
			get { return _logFile; }
			private set
			{
				if (value == _logFile)
					return;

				_logFile = value;
				EmitPropertyChanged();
			}
		}

		public ILogFileSearch Search
		{
			get { return _search; }
			private set
			{
				if (value == _search)
					return;

				_search = value;
				EmitPropertyChanged();
			}
		}

		public string NoEntriesSubtext
		{
			get { return _noEntriesSubtext; }
			private set
			{
				if (Equals(value, _noEntriesSubtext))
					return;

				_noEntriesSubtext = value;
				EmitPropertyChanged();
			}
		}

		public string NoEntriesExplanation
		{
			get { return _noEntriesExplanation; }
			private set
			{
				if (Equals(value, _noEntriesExplanation))
					return;

				_noEntriesExplanation = value;
				EmitPropertyChanged();
			}
		}

		public int LogEntryCount
		{
			get { return _logEntryCount; }
			private set
			{
				if (value == _logEntryCount)
					return;

				_logEntryCount = value;
				EmitPropertyChanged();
			}
		}

		public int TotalLogEntryCount
		{
			get { return _totalLogEntryCount; }
			private set
			{
				if (value == _totalLogEntryCount)
					return;

				_totalLogEntryCount = value;
				EmitPropertyChanged();
			}
		}

		public IDataSourceViewModel DataSource
		{
			get { return _dataSource; }
		}

		/// <summary>
		///     The list of filters as produced by the "quick filter" panel.
		/// </summary>
		public IEnumerable<ILogEntryFilter> QuickFilterChain
		{
			get
			{
				IDataSourceViewModel source = _dataSource;
				if (source == null)
					return null;

				return source.QuickFilterChain;
			}
			set
			{
				if (value == QuickFilterChain)
					return;

				if (_dataSource != null)
				{
					_dataSource.QuickFilterChain = value;
				}
			}
		}

		public void OnLogFileModified(ILogFile logFile, LogFileSection section)
		{
			lock (_pendingSections)
			{
				if (section == LogFileSection.Reset)
					_pendingSections.Clear();

				_pendingSections.Add(new KeyValuePair<ILogFile, LogFileSection>(_logFile, section));
				_dispatcher.BeginInvoke(Synchronize, DispatcherPriority.Background);
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void EmitPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChangedEventHandler handler = PropertyChanged;
			if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
		}

		private void Synchronize()
		{
			lock (_pendingSections)
			{
				foreach (var pair in _pendingSections)
				{
					ILogFile file = pair.Key;
					if (file != _logFile)
						continue; //< This message belongs to an old change and must be ignored


					LogEntryCount = 0;
					TotalLogEntryCount = 0;
				}

				_pendingSections.Clear();
			}

			UpdateCounts();
		}

		private void UpdateCounts()
		{
			LogEntryCount = _logFile.Count;
			TotalLogEntryCount = _dataSource.DataSource.UnfilteredLogFile.Count;
			UpdateNoEntriesExplanation();
		}

		private void UpdateNoEntriesExplanation()
		{
			IDataSource dataSource = _dataSource.DataSource;
			ILogFile source = dataSource.UnfilteredLogFile;
			ILogFile filtered = dataSource.FilteredLogFile;

			if (filtered.Count == 0)
			{
				IEnumerable<ILogEntryFilter> chain = dataSource.QuickFilterChain;
				if (!source.Exists)
				{
					NoEntriesExplanation = string.Format("Can't find \"{0}\"", Path.GetFileName(dataSource.FullFileName));
					NoEntriesSubtext = string.Format("It was last seen at {0}", Path.GetDirectoryName(dataSource.FullFileName));
				}
				else if (source.FileSize == Size.Zero)
				{
					NoEntriesExplanation = "The data source is empty";
					NoEntriesSubtext = null;
				}
				else if (dataSource.LevelFilter == LevelFlags.None)
				{
					NoEntriesExplanation = "Not a single log entry matches the level selection";
					NoEntriesSubtext = null;
				}
				else if (!string.IsNullOrEmpty(dataSource.SearchTerm))
				{
					NoEntriesExplanation = "Not a single log entry matches the log file filter";
					NoEntriesSubtext = null;
				}
				else if (chain != null && chain.All(x => x != null))
				{
					NoEntriesExplanation = "Not a single log entry matches the activated quick filters";
					NoEntriesSubtext = null;
				}
				else
				{
					NoEntriesExplanation = null;
					NoEntriesSubtext = null;
				}
			}
			else
			{
				NoEntriesExplanation = null;
				NoEntriesSubtext = null;
			}
		}
	}
}