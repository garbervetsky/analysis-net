﻿// Copyright (c) Edgardo Zoppi.  All Rights Reserved.  Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

using Backend.ThreeAddressCode;
using Backend.Utils;
using Backend.ThreeAddressCode.Instructions;
using Backend.Visitors;

namespace Backend.Model
{
	public enum CFGNodeKind
	{
		Entry,
		Exit,
		NormalExit,
		ExceptionalExit,
		BasicBlock
	}

	public enum CFGRegionKind
	{
		Try,
		Catch,
		Fault,
		Finally,
		Loop
	}

	public abstract class CFGRegion
	{
		public abstract CFGRegionKind Kind { get; }
		public CFGNode Header { get; set; }
		public ISet<CFGNode> Nodes { get; private set; }

		public CFGRegion()
		{
			this.Nodes = new HashSet<CFGNode>();
		}

		public CFGRegion(CFGNode header)
			: this()
		{
			this.Header = header;
			this.Nodes.Add(header);
		}

		public override string ToString()
		{
			var sb = new StringBuilder();

			sb.AppendFormat("{0} ", this.Kind);

			foreach (var node in this.Nodes)
			{
				sb.AppendFormat("B{0} ", node.Id);
			}

			return sb.ToString();
		}
	}

	public class CFGProtectedRegion : CFGRegion
	{
		public CFGExceptionHandlerRegion Handler { get; set; }

		public override CFGRegionKind Kind
		{
			get { return CFGRegionKind.Try; }
		}

		public override string ToString()
		{
			var self = base.ToString();
			return string.Format("{0} {1}", self, this.Handler);
		}
	}

	public class CFGExceptionHandlerRegion : CFGRegion
	{
		private CFGRegionKind kind;

		public CFGProtectedRegion ProtectedRegion { get; set; }

		public CFGExceptionHandlerRegion(CFGRegionKind kind)
		{
			this.kind = kind;
		}

		public override CFGRegionKind Kind
		{
			get { return kind; }
		}
	}

	public class CFGLoop : CFGRegion
	{
		//public IExpression Condition { get; set; }

		public CFGLoop(CFGNode header)
			: base(header)
		{
		}

		public override CFGRegionKind Kind
		{
			get { return CFGRegionKind.Loop; }
		}
	}

	public class CFGEdge
	{
		public CFGNode Source { get; set; }
		public CFGNode Target { get; set; }

		public CFGEdge(CFGNode source, CFGNode target)
		{
			this.Source = source;
			this.Target = target;
		}

		public override string ToString()
		{
			return string.Format("{0} -> {1}", this.Source, this.Target);
		}
	}

	public class CFGNode : IInstructionContainer
	{
		private ISet<CFGNode> dominators;

		public const int FirstAvailableId = 4;

		public int Id { get; private set; }
		public int ForwardIndex { get; set; }
		public int BackwardIndex { get; set; }
		public CFGNodeKind Kind { get; private set; }
		public ISet<CFGNode> Predecessors { get; private set; }
		public ISet<CFGNode> Successors { get; private set; }
		public IList<Instruction> Instructions { get; private set; }
		public CFGNode ImmediateDominator { get; set; }
		public ISet<CFGNode> ImmediateDominated { get; private set; }
		public ISet<CFGNode> DominanceFrontier { get; private set; }

		public CFGNode(int id, CFGNodeKind kind = CFGNodeKind.BasicBlock)
		{
			this.Id = id;
			this.Kind = kind;
			this.ForwardIndex = -1;
			this.BackwardIndex = -1;
			this.Predecessors = new HashSet<CFGNode>();
			this.Successors = new HashSet<CFGNode>();
			this.Instructions = new List<Instruction>();
			this.ImmediateDominated = new HashSet<CFGNode>();
			this.DominanceFrontier = new HashSet<CFGNode>();
		}

		public ISet<CFGNode> Dominators
		{
			get
			{
				if (this.dominators == null)
				{
					this.dominators = ComputeDominators(this);
				}

				return this.dominators;
			}
		}

		public override string ToString()
		{
			string result;

			switch (this.Kind)
			{
				case CFGNodeKind.Entry: result = "entry"; break;
				case CFGNodeKind.Exit: result = "exit"; break;
				case CFGNodeKind.NormalExit: result = "normal exit"; break;
				case CFGNodeKind.ExceptionalExit: result = "exceptional exit"; break;
				default: result = string.Join("\n", this.Instructions); break;
			}

			return result;
		}

		private static ISet<CFGNode> ComputeDominators(CFGNode node)
		{
			var result = new HashSet<CFGNode>();

			do
			{
				result.Add(node);
				node = node.ImmediateDominator;
			}
			while (node != null);

			return result;
		}
	}

	public class ControlFlowGraph
	{
		private CFGNode[] forwardOrder;
		private CFGNode[] backwardOrder;

		public CFGNode Entry { get; private set; }
		public CFGNode Exit { get; private set; }
		public CFGNode NormalExit { get; private set; }
		public CFGNode ExceptionalExit { get; private set; }
		public ISet<CFGNode> Nodes { get; private set; }
		public ISet<CFGRegion> Regions { get; private set; }

		public ControlFlowGraph()
		{
			this.Entry = new CFGNode(0, CFGNodeKind.Entry);
			this.Exit = new CFGNode(1, CFGNodeKind.Exit);
			this.NormalExit = new CFGNode(2, CFGNodeKind.NormalExit);
			this.ExceptionalExit = new CFGNode(3, CFGNodeKind.ExceptionalExit);
			this.Regions = new HashSet<CFGRegion>();
			this.Nodes = new HashSet<CFGNode>()
			{
				this.Entry,
				this.Exit,
				this.NormalExit,
				this.ExceptionalExit
			};

			this.ConnectNodes(this.NormalExit, this.Exit);
			this.ConnectNodes(this.ExceptionalExit, this.Exit);
		}

		public IEnumerable<CFGNode> Entries
		{
			get
			{
				var result = from node in this.Nodes
							 where node.Predecessors.Count == 0
							 select node;

				return result;
			}
		}

		public IEnumerable<CFGNode> Exits
		{
			get
			{
				var result = from node in this.Nodes
							 where node.Successors.Count == 0
							 select node;

				return result;
			}
		}

		public CFGNode[] ForwardOrder
		{
			get
			{
				if (this.forwardOrder == null)
				{
					this.forwardOrder = this.ComputeForwardTopologicalSort();
				}

				return this.forwardOrder;
			}
		}

		public CFGNode[] BackwardOrder
		{
			get
			{
				if (this.backwardOrder == null)
				{
					this.backwardOrder = this.ComputeBackwardTopologicalSort();
				}

				return this.backwardOrder;
			}
		}

		public void ConnectNodes(CFGNode predecessor, CFGNode successor)
				{
			successor.Predecessors.Add(predecessor);
			predecessor.Successors.Add(successor);
			this.Nodes.Add(predecessor);
			this.Nodes.Add(successor);
		}

		#region Topological Sort

		//private CFGNode[] ComputeForwardTopologicalSort()
		//{
		//	var result = new CFGNode[this.Nodes.Count];
		//	var visited = new bool[this.Nodes.Count];
		//	var index = this.Nodes.Count - 1;

		//	foreach (var node in this.Entries)
		//	{
		//		ControlFlowGraph.DepthFirstSearch(result, visited, node, ref index);
		//	}

		//	return result;
		//}

		//private static void DepthFirstSearch(CFGNode[] result, bool[] visited, CFGNode node, ref int index)
		//{
		//	var alreadyVisited = visited[node.Id];

		//	if (!alreadyVisited)
		//	{
		//		visited[node.Id] = true;

		//		foreach (var succ in node.Successors)
		//		{
		//			ControlFlowGraph.DepthFirstSearch(result, visited, succ, ref index);
		//		}

		//		node.ForwardIndex = index;
		//		result[index] = node;
		//		index--;
		//	}
		//}

		private enum TopologicalSortNodeStatus
		{
			NeverVisited, // never pushed into stack
			FirstVisit, // pushed into stack for the first time
			SecondVisit // pushed into stack for the second time
		}

		private CFGNode[] ComputeForwardTopologicalSort()
		{
			// reverse postorder traversal from entry node
			var stack = new Stack<CFGNode>();
			var result = new CFGNode[this.Nodes.Count];
			var status = new TopologicalSortNodeStatus[this.Nodes.Count];
			var index = this.Nodes.Count - 1;

			foreach (var node in this.Entries)
			{
				stack.Push(node);
				status[node.Id] = TopologicalSortNodeStatus.FirstVisit;
			}

			do
			{
				var node = stack.Peek();
				var node_status = status[node.Id];

				if (node_status == TopologicalSortNodeStatus.FirstVisit)
				{
					status[node.Id] = TopologicalSortNodeStatus.SecondVisit;

					foreach (var succ in node.Successors)
					{
						var succ_status = status[succ.Id];

						if (succ_status == TopologicalSortNodeStatus.NeverVisited)
						{
							stack.Push(succ);
							status[succ.Id] = TopologicalSortNodeStatus.FirstVisit;
						}
					}
				}
				else if (node_status == TopologicalSortNodeStatus.SecondVisit)
				{
					stack.Pop();
					node.ForwardIndex = index;
					result[index] = node;
					index--;
				}
			}
			while (stack.Count > 0);

			return result;
		}

		private CFGNode[] ComputeBackwardTopologicalSort()
		{
			// reverse postorder traversal from exit node
			var stack = new Stack<CFGNode>();
			var result = new CFGNode[this.Nodes.Count];
			var status = new TopologicalSortNodeStatus[this.Nodes.Count];
			var index = this.Nodes.Count - 1;

			foreach (var node in this.Exits)
			{
				stack.Push(node);
				status[node.Id] = TopologicalSortNodeStatus.FirstVisit;
			}

			do
			{
				var node = stack.Peek();
				var node_status = status[node.Id];

				if (node_status == TopologicalSortNodeStatus.FirstVisit)
				{
					status[node.Id] = TopologicalSortNodeStatus.SecondVisit;

					foreach (var pred in node.Predecessors)
					{
						var pred_status = status[pred.Id];

						if (pred_status == TopologicalSortNodeStatus.NeverVisited)
						{
							stack.Push(pred);
							status[pred.Id] = TopologicalSortNodeStatus.FirstVisit;
						}
					}
				}
				else if (node_status == TopologicalSortNodeStatus.SecondVisit)
				{
					stack.Pop();
					node.BackwardIndex = index;
					result[index] = node;
					index--;
				}
			}
			while (stack.Count > 0);

			return result;
		}

		#endregion
	}
}
