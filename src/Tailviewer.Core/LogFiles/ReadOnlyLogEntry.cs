﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Tailviewer.BusinessLogic.LogFiles;

namespace Tailviewer.Core.LogFiles
{
	/// <summary>
	///     Represents a log entry which cannot be modified after it's been constructed.
	/// </summary>
	/// <remarks>
	///     Don't keep many instances of this type in memory as it wastes a lot thereof.
	/// </remarks>
	public sealed class ReadOnlyLogEntry
		: AbstractReadOnlyLogEntry
	{
		private static readonly IReadOnlyLogEntry _empty;

		static ReadOnlyLogEntry()
		{
			_empty = new ReadOnlyLogEntry(new Dictionary<ILogFileColumn, object>());
		}

		private readonly IReadOnlyDictionary<ILogFileColumn, object> _values;

		/// <summary>
		/// </summary>
		/// <param name="values"></param>
		/// <exception cref="ArgumentNullException"></exception>
		public ReadOnlyLogEntry(IReadOnlyDictionary<ILogFileColumn, object> values)
		{
			if (values == null)
				throw new ArgumentNullException(nameof(values));

			_values = values;
		}

		/// <inheritdoc />
		public override T GetValue<T>(ILogFileColumn<T> column)
		{
			return (T) GetValue((ILogFileColumn) column);
		}

		/// <inheritdoc />
		public override bool TryGetValue<T>(ILogFileColumn<T> column, out T value)
		{
			object tmp;
			if (!TryGetValue(column, out tmp))
			{
				value = column.DefaultValue;
				return false;
			}

			value = (T) tmp;
			return true;
		}

		/// <inheritdoc />
		public override object GetValue(ILogFileColumn column)
		{
			object value;
			if (!TryGetValue(column, out value))
				throw new NoSuchColumnException(column);

			return value;
		}

		/// <inheritdoc />
		public override bool TryGetValue(ILogFileColumn column, out object value)
		{
			if (!_values.TryGetValue(column, out value))
			{
				value = column.DefaultValue;
				return false;
			}

			return true;
		}

		/// <inheritdoc />
		public override IReadOnlyList<ILogFileColumn> Columns => _values.Keys.ToList();

		/// <summary>
		///     A completely empty log entry.
		/// </summary>
		public static IReadOnlyLogEntry Empty => _empty;

		/// <summary>
		/// </summary>
		/// <param name="columns"></param>
		/// <param name="values"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		/// <exception cref="ArgumentException"></exception>
		[Pure]
		public static IReadOnlyLogEntry Create(IReadOnlyList<ILogFileColumn> columns,
		                                       IReadOnlyList<object> values)
		{
			if (columns == null)
				throw new ArgumentNullException(nameof(columns));
			if (values == null)
				throw new ArgumentNullException(nameof(values));
			if (columns.Count != values.Count)
				throw new
					ArgumentOutOfRangeException(string.Format("Expected number of columns '{0}' to match number of values '{1}'",
					                                          columns.Count, values.Count));

			var valuesPerColumn = new Dictionary<ILogFileColumn, object>(columns.Count);
			for (var i = 0; i < columns.Count; ++i)
			{
				var column = columns[i];
				var value = values[i];
				if (value != null && !column.DataType.IsInstanceOfType(value))
					throw new ArgumentException(string.Format("Expected value '{0}' to be of type '{1}' but it is not",
					                                          value,
					                                          column.DataType));

				valuesPerColumn.Add(columns[i], values[i]);
			}
			return new ReadOnlyLogEntry(valuesPerColumn);
		}

		/// <summary>
		///     Creates a new log entry which only contains values for the given <paramref name="columns" />.
		///     Values of other columns are ignored.
		/// </summary>
		/// <param name="columns"></param>
		/// <param name="values"></param>
		/// <returns></returns>
		/// <exception cref="NotImplementedException"></exception>
		[Pure]
		public static IReadOnlyLogEntry Create(IReadOnlyList<ILogFileColumn> columns,
		                                       IReadOnlyDictionary<ILogFileColumn, object> values)
		{
			var actualValues = new List<object>();
			foreach (var column in columns)
			{
				object value;
				if (values.TryGetValue(column, out value))
				{
					actualValues.Add(value);
				}
				else
				{
					actualValues.Add(column.DefaultValue);
				}
			}

			return Create(columns, actualValues);
		}
	}
}