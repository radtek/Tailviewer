using System.Collections.Generic;
using Tailviewer.BusinessLogic.Bookmarks;

namespace Tailviewer.BusinessLogic.DataSources
{
	public interface IDataSources
		: IEnumerable<IDataSource>
	{
		#region Bookmarks

		/// <summary>
		/// 
		/// </summary>
		/// <param name="dataSource"></param>
		/// <param name="orignalLogLineIndex"></param>
		/// <returns></returns>
		Bookmark TryAddBookmark(IDataSource dataSource, LogLineIndex orignalLogLineIndex);

		/// <summary>
		/// 
		/// </summary>
		IReadOnlyList<Bookmark> Bookmarks { get; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="bookmark"></param>
		void RemoveBookmark(Bookmark bookmark);

		#endregion

		#region Datasources

		SingleDataSource AddDataSource(string fileName);
		MergedDataSource AddGroup();
		bool Remove(IDataSource viewModelDataSource);

		#endregion
	}
}