using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Structure
{	
	[Serializable()]
	public class PairNode<T>
	{
		public delegate bool TraversalDataDelegate(T data);
		public delegate bool TraversalNodeDelegate(PairNode<T> node);

		private readonly T _data;
		private readonly PairNode<T> _parent;
		private readonly int _level;
		private readonly List<PairNode<T>> _children;

		public PairNode()
        {
			_data = default(T);
			_children = new List<PairNode<T>>();
			_level = 0;
		}

		public PairNode(T data)
		{
			_data = data;
			_children = new List<PairNode<T>>();
			_level = 0;
		}

		public PairNode(T data, PairNode<T> parent) : this(data)
		{
			_parent = parent;
			_level = _parent != null ? _parent.Level + 1 : 0;
		}

		public int Level { get { return _level; } }
		public int Count { get { return _children.Count; } }
		public bool IsRoot { get { return _parent == null; } }
		public bool IsLeaf { get { return _children.Count == 0; } }
		public T Data { get { return _data; } }
		public PairNode<T> Parent { get { return _parent; } }
		public List<PairNode<T>> Children { get { return _children; } }
		public PairNode<T> this[int key]
		{
			get { return _children[key]; }
		}

		public void Clear()
		{
			_children.Clear();
		}

		public PairNode<T> AddChild(T value)
		{
			PairNode<T> node = new PairNode<T>(value, this);
			_children.Add(node);

			return node;
		}

		public bool HasChild(T data)
		{
			return FindInChildren(data) != null;
		}

		public PairNode<T> FindInChildren(T data)
		{
			int i = 0, l = Count;
			for (; i < l; ++i)
			{
				PairNode<T> child = _children[i];
				if (child.Data.Equals(data)) return child;
			}

			return null;
		}

		public bool RemoveChild(PairNode<T> node)
		{
			return _children.Remove(node);
		}

		public void Traverse(TraversalDataDelegate handler)
		{
			if (handler(_data))
			{
				int i = 0, l = Count;
				for (; i < l; ++i) _children[i].Traverse(handler);
			}
		}

		public void Traverse(TraversalNodeDelegate handler)
		{
			if (handler(this))
			{
				int i = 0, l = Count;
				for (; i < l; ++i) _children[i].Traverse(handler);
			}
		}

        public override string ToString()
        {
			return Data.ToString();
        }
	}
}
