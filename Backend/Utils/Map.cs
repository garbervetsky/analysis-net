﻿// Copyright (c) Edgardo Zoppi.  All Rights Reserved.  Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Backend.Model;
using Model;

namespace Backend.Utils
{
	public class Map<TKey, TValue, TCollection> : Dictionary<TKey, TCollection>
		where TCollection : ICollection<TValue>, new()
	{
		public void Add(TKey key)
		{
			this.AddKeyAndGetValues(key);
		}

		public void Add(TKey key, TValue value)
		{
			var collection = this.AddKeyAndGetValues(key);
			collection.Add(value);
		}

		public void AddRange(TKey key, IEnumerable<TValue> values)
		{
			var collection = this.AddKeyAndGetValues(key);
			collection.AddRange(values);
		}

		private TCollection AddKeyAndGetValues(TKey key)
		{
			TCollection result;

			if (this.ContainsKey(key))
			{
				result = this[key];
			}
			else
			{
				result = new TCollection();
				this.Add(key, result);
			}

			return result;
		}
	}

	public class MapSet<TKey, TValue> : Map<TKey, TValue, HashSet<TValue>>
	{
        public MapSet(): base()
        {
        }

        public MapSet(MapSet<TKey, TValue> map)
        {
            this.AddRange(map);
        }

        public bool MapEquals(MapSet<TKey, TValue> other)
		{
			Func<ISet<TValue>, ISet<TValue>, bool> setEquals = (a, b) => a.SetEquals(b);
			return this.DictionaryEquals(other, setEquals);
		}
	}

	public class MapList<TKey, TValue> : Map<TKey, TValue, List<TValue>>
	{
		public bool MapEquals(MapList<TKey, TValue> other)
		{
			Func<IList<TValue>, IList<TValue>, bool> listEquals = (a, b) => a.SequenceEqual(b);
			return this.DictionaryEquals(other, listEquals);
		}
	}
}
