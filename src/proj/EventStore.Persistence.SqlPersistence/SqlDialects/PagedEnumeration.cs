namespace EventStore.Persistence.SqlPersistence.SqlDialects
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Data;

	public class PagedEnumeration<T> : IEnumerable<T>, IEnumerator<T>
	{
		private readonly IDbCommand command;
		private readonly Func<IDataRecord, T> select;
		private readonly NextPageDelegate<T> onNextPage;
		private readonly int pageSize;
		private IDataReader reader;
		private int position;
		private T latest;

		public PagedEnumeration(
			IDbCommand command, Func<IDataRecord, T> select, NextPageDelegate<T> onNextPage, int pageSize)
		{
			this.command = command;
			this.select = select;
			this.onNextPage = onNextPage;
			this.pageSize = pageSize;
			this.latest = default(T);
		}

		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}
		protected virtual void Dispose(bool disposing)
		{
			if (disposing && this.reader != null)
				this.reader.Dispose();

			this.reader = null;
			this.position = 0;
			this.latest = default(T);
		}

		public virtual IEnumerator<T> GetEnumerator()
		{
			return this;
		}
		IEnumerator IEnumerable.GetEnumerator()
		{
			return this;
		}

		bool IEnumerator.MoveNext()
		{
			this.reader = this.reader ?? this.OpenNextPage();

			if (this.reader.Read())
				return this.IncrementPosition();

			if (!this.PagingEnabled())
				return false;

			if (!this.PageCompletelyEnumerated())
				return false;

			this.reader.Dispose();
			this.reader = this.OpenNextPage();

			if (this.reader.Read())
				return this.IncrementPosition();

			return false;
		}
		private bool IncrementPosition()
		{
			this.position++;
			return true;
		}

		private bool PagingEnabled()
		{
			return this.pageSize > 0;
		}
		private bool PageCompletelyEnumerated()
		{
			return this.position > 0 && 0 == this.position % this.pageSize;
		}
		private IDataReader OpenNextPage()
		{
			if (this.pageSize > 0 && this.position >= this.pageSize)
				this.onNextPage(this.command, this.latest);

			try
			{
				return this.command.ExecuteReader();
			}
			catch (Exception e)
			{
				throw new StorageUnavailableException(e.Message, e);
			}
		}

		void IEnumerator.Reset()
		{
			throw new NotSupportedException("Forward-only readers.");
		}
		T IEnumerator<T>.Current
		{
			get { return this.latest = this.select(this.reader); }
		}
		object IEnumerator.Current
		{
			get { return ((IEnumerator<IDataRecord>)this).Current; }
		}
	}
}