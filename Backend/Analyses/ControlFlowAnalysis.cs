﻿// Copyright (c) Edgardo Zoppi.  All Rights Reserved.  Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Model;
using Model.Types;
using Model.ThreeAddressCode;
using Bytecode = Model.Bytecode;
using Tac = Model.ThreeAddressCode.Instructions;
using Backend.Model;
using Backend.Utils;

namespace Backend.Analyses
{
	public class ControlFlowAnalysis
	{
		private MethodBody methodBody;
		private MapSet<string, ExceptionHandlerBlock> exceptionHandlersStart;

		public ControlFlowAnalysis(MethodBody methodBody)
		{
			this.methodBody = methodBody;
		}

		public ControlFlowGraph GenerateNormalControlFlow()
		{
			exceptionHandlersStart = GetExceptionHandlersStart();

			var instructions = FilterExceptionHandlers();
			var leaders = CreateNodes(instructions);
			var cfg = ConnectNodes(instructions, leaders);

			return cfg;
		}

		public ControlFlowGraph GenerateExceptionalControlFlow()
		{
			exceptionHandlersStart = GetExceptionHandlersStart();

			var instructions = methodBody.Instructions;
			var leaders = CreateNodes(instructions);
			var cfg = ConnectNodes(instructions, leaders);

			CreateExceptionHandlerRegions(cfg, leaders);
			ConnectNodesWithExceptionHandlers(cfg);

			return cfg;
		}

		private MapSet<string, ExceptionHandlerBlock> GetExceptionHandlersStart()
		{
			var result = new MapSet<string, ExceptionHandlerBlock>();

			foreach (var protectedBlock in methodBody.ExceptionInformation)
			{
				result.Add(protectedBlock.Start, protectedBlock);
				result.Add(protectedBlock.Handler.Start, protectedBlock.Handler);
			}

			return result;
		}

		private MapSet<string, ExceptionHandlerBlock> GetExceptionHandlersEnd()
		{
			var result = new MapSet<string, ExceptionHandlerBlock>();

			foreach (var protectedBlock in methodBody.ExceptionInformation)
			{
				result.Add(protectedBlock.End, protectedBlock);
				result.Add(protectedBlock.Handler.End, protectedBlock.Handler);
			}

			return result;
		}

		private IList<IInstruction> FilterExceptionHandlers()
		{
			var instructions = new List<IInstruction>();
			var handlersRange = methodBody.ExceptionInformation.ToDictionary(pb => pb.Handler.Start, pb => pb.Handler.End);
			var i = 0;

			while (i < methodBody.Instructions.Count)
			{
				var instruction = methodBody.Instructions[i];

				if (handlersRange.ContainsKey(instruction.Label))
				{
					var handlerEnd = handlersRange[instruction.Label];

					do
					{
						i++;
						instruction = methodBody.Instructions[i];
					}
					while (!instruction.Label.Equals(handlerEnd));
				}
				else
				{
					instructions.Add(instruction);
					i++;
				}
			}

			return instructions;
		}

		private IDictionary<string, CFGNode> CreateNodes(IEnumerable<IInstruction> instructions)
		{
			var leaders = new Dictionary<string, CFGNode>();
			var nextIsLeader = true;
			var nodeId = CFGNode.FirstAvailableId;

			foreach (var instruction in instructions)
			{
				var isLeader = nextIsLeader || IsLeader(instruction);
				nextIsLeader = false;

				if (isLeader && !leaders.ContainsKey(instruction.Label))
				{
					var node = new CFGNode(nodeId++);
					leaders.Add(instruction.Label, node);
				}

				IList<string> targets;
				var isBranch = IsBranch(instruction, out targets);
				var isExitingMethod = IsExitingMethod(instruction);

				if (isBranch)
				{
					nextIsLeader = true;

					foreach (var target in targets)
					{
						if (!leaders.ContainsKey(target))
						{
							var node = new CFGNode(nodeId++);
							leaders.Add(target, node);
						}
					}
				}
				else if (isExitingMethod)
				{
					nextIsLeader = true;
				}
			}

			return leaders;
		}

		private static ControlFlowGraph ConnectNodes(IEnumerable<IInstruction> instructions, IDictionary<string, CFGNode> leaders)
		{
			var cfg = new ControlFlowGraph();
			var connectWithPreviousNode = true;
			var current = cfg.Entry;
			CFGNode previous;

			foreach (var instruction in instructions)
			{
				if (leaders.ContainsKey(instruction.Label))
				{
					previous = current;
					current = leaders[instruction.Label];

					// A node cannot fall-through itself,
					// unless it contains another
					// instruction with the same label 
					// of the node's leader instruction
					if (connectWithPreviousNode && previous.Id != current.Id)
					{
						cfg.ConnectNormalNodes(previous, current);
					}
				}

				current.Instructions.Add(instruction);
				connectWithPreviousNode = CanFallThroughNextInstruction(instruction);

				IList<string> targets;
				var isBranch = IsBranch(instruction, out targets);
				var isExitingMethod = IsExitingMethod(instruction);

				if (isBranch)
				{
					foreach (var label in targets)
					{
						var target = leaders[label];

						cfg.ConnectNormalNodes(current, target);
					}
				}
				else if (isExitingMethod)
				{
					//TODO: not always connect to exit, could exists a catch or finally block
					cfg.ConnectNormalNodes(current, cfg.NormalExit);
				}
			}

			cfg.ConnectNormalNodes(current, cfg.NormalExit);
			return cfg;
		}

		private void CreateExceptionHandlerRegions(ControlFlowGraph cfg, IDictionary<string, CFGNode> leaders)
		{
			var activeRegions = new List<CFGRegion>();
			var exceptionHandlerRegions = new Dictionary<ExceptionHandlerBlock, CFGRegion>();

			var exceptionHandlersEnd = GetExceptionHandlersEnd();
			var orderedLeaders = from entry in leaders
								 orderby entry.Value.StartOffset()
								 select entry;

			foreach (var protectedBlock in methodBody.ExceptionInformation)
			{
				CFGExceptionHandlerRegion handler;
				var protectedRegion = new CFGProtectedRegion();

				switch (protectedBlock.Handler.Kind)
				{
					case ExceptionHandlerBlockKind.Catch:
						handler = new CFGExceptionHandlerRegion(CFGRegionKind.Catch);
						break;

					case ExceptionHandlerBlockKind.Fault:
						handler = new CFGExceptionHandlerRegion(CFGRegionKind.Fault);
						break;

					case ExceptionHandlerBlockKind.Finally:
						handler = new CFGExceptionHandlerRegion(CFGRegionKind.Finally);
						break;

					default:
						throw new NotImplementedException();
				}

				handler.ProtectedRegion = protectedRegion;
				protectedRegion.Handler = handler;

				cfg.Regions.Add(protectedRegion);
				cfg.Regions.Add(protectedRegion.Handler);

				exceptionHandlerRegions.Add(protectedBlock, protectedRegion);
				exceptionHandlerRegions.Add(protectedBlock.Handler, protectedRegion.Handler);
			}

			foreach (var entry in orderedLeaders)
			{
				HashSet<ExceptionHandlerBlock> exceptionHandlers;
				var label = entry.Key;
				var node = entry.Value;

				if (exceptionHandlersStart.TryGetValue(label, out exceptionHandlers))
				{
					foreach (var exceptionHandler in exceptionHandlers)
					{
						var region = exceptionHandlerRegions[exceptionHandler];
						activeRegions.Add(region);
						region.Header = node;
					}
				}

				if (exceptionHandlersEnd.TryGetValue(label, out exceptionHandlers))
				{
					foreach (var exceptionHandler in exceptionHandlers)
					{
						var region = exceptionHandlerRegions[exceptionHandler];
						activeRegions.Remove(region);
					}
				}

				foreach (var region in activeRegions)
				{
					region.Nodes.Add(node);
				}
			}
		}

		private void ConnectNodesWithExceptionHandlers(ControlFlowGraph cfg)
		{
			var protectedRegions = cfg.GetProtectedRegions();

			foreach (var protectedRegion in protectedRegions)
			{
				foreach (var node in protectedRegion.Nodes)
				{
					if (protectedRegion.Handler.Header != null)
					{
						cfg.ConnecExceptionalNodes(node, protectedRegion.Handler.Header);
					}
				}
			}
		}

		//private void ConnectNodesWithExceptionHandlers(ControlFlowGraph cfg, IDictionary<string, CFGNode> leaders)
		//{
		//	var activeProtectedBlocks = new HashSet<ProtectedBlock>();
		//	var protectedBlocksStart = methodBody.ExceptionInformation.ToLookup(pb => pb.Start);
		//	var protectedBlocksEnd = methodBody.ExceptionInformation.ToLookup(pb => pb.End);

		//	var orderedLeaders = from entry in leaders
		//						 orderby entry.Value.StartOffset()
		//						 select entry;

		//	foreach (var entry in orderedLeaders)
		//	{
		//		var label = entry.Key;
		//		var node = entry.Value;

		//		if (protectedBlocksStart.Contains(label))
		//		{
		//			var startingProtectedBlocks = protectedBlocksStart[label];
		//			activeProtectedBlocks.UnionWith(startingProtectedBlocks);
		//		}

		//		if (protectedBlocksEnd.Contains(label))
		//		{
		//			var endingProtectedBlocks = protectedBlocksEnd[label];
		//			activeProtectedBlocks.ExceptWith(endingProtectedBlocks);
		//		}

		//		// Connect each node inside a try block to the first corresponding handler block
		//		foreach (var block in activeProtectedBlocks)
		//		{
		//			var target = leaders[block.Handler.Start];
		//			cfg.ConnectNodes(node, target);
		//		}
		//	}
		//}

		private bool IsLeader(IInstruction instruction)
		{
			var result = false;

			// Bytecode
			if (instruction is Bytecode.Instruction)
			{
				result = exceptionHandlersStart.ContainsKey(instruction.Label);
			}
			// TAC
			else
			{
				result = instruction is Tac.TryInstruction ||
						 instruction is Tac.CatchInstruction ||
						 instruction is Tac.FinallyInstruction;
			}

			return result;
		}

		private static bool IsBranch(IInstruction instruction, out IList<string> targets)
		{
			var result = false;
			targets = null;

			// Bytecode
			if (instruction is Bytecode.BranchInstruction)
			{
				var branch = instruction as Bytecode.BranchInstruction;

				targets = new List<string>() { branch.Target };
				result = true;
			}
			else if (instruction is Bytecode.SwitchInstruction)
			{
				var branch = instruction as Bytecode.SwitchInstruction;

				targets = branch.Targets;
				result = true;
			}
			// TAC
			else if (instruction is Tac.UnconditionalBranchInstruction ||
					 instruction is Tac.ConditionalBranchInstruction)
			{
				var branch = instruction as Tac.BranchInstruction;

				targets = new List<string>() { branch.Target };
				result = true;
			}
			else if (instruction is Tac.SwitchInstruction)
			{
				var branch = instruction as Tac.SwitchInstruction;

				targets = branch.Targets;
				result = true;
			}

			return result;
		}

		private static bool IsExitingMethod(IInstruction instruction)
		{
			var result = false;

			// Bytecode
			if (instruction is Bytecode.BasicInstruction)
			{
				var basic = instruction as Bytecode.BasicInstruction;

				result = basic.Operation == Bytecode.BasicOperation.Return ||
						 basic.Operation == Bytecode.BasicOperation.Throw ||
						 basic.Operation == Bytecode.BasicOperation.Rethrow;
			}
			// TAC
			else
			{
				result = instruction is Tac.ReturnInstruction ||
						 instruction is Tac.ThrowInstruction;
			}

			return result;
		}

		private static bool CanFallThroughNextInstruction(IInstruction instruction)
		{
			var result = false;

			// Bytecode
			if (instruction is Bytecode.BranchInstruction)
			{
				var branch = instruction as Bytecode.BranchInstruction;

				result = branch.Operation == Bytecode.BranchOperation.Branch ||
						 branch.Operation == Bytecode.BranchOperation.Leave;
			}
			// TAC
			else
			{
				result = instruction is Tac.UnconditionalBranchInstruction;
			}

			result = result || IsExitingMethod(instruction);
			return !result;
		}
	}
}
