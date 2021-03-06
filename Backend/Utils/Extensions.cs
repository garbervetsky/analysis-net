﻿// Copyright (c) Edgardo Zoppi.  All Rights Reserved.  Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Model.ThreeAddressCode.Values;
using Model.ThreeAddressCode.Expressions;
using Model.ThreeAddressCode.Instructions;
using Backend.Analyses;
using Model.Types;
using Model;
using Backend.Model;
using Backend.Transformations;

namespace Backend.Utils
{
	public static class Extensions
	{
        public static bool MapLessEquals<K, V>(this MapSet<K, V> left, MapSet<K, V> right)
        {
            var result = false;
            if (!left.Keys.Except(right.Keys).Any() && left.Keys.Count() <= right.Keys.Count())
            {
                return left.All(kv => kv.Value.All(n => right[kv.Key].Contains(n)));
                    //&& right.All(kv => kv.Value.IsSubsetOf(left[kv.Key]));
            }
            return result;
        }


        public static bool DictionaryEquals<K,V>(this IDictionary<K,V> self, IDictionary<K,V> other, Func<V, V, bool> valueEquals = null)
		{
			if (object.ReferenceEquals(self, other)) return true;
			if (self.Count != other.Count) return false;

			if (valueEquals == null)
			{
				valueEquals = (a, b) => object.Equals(a, b);
			}

			foreach (var key in self.Keys)
			{
				var otherContainsKey = other.ContainsKey(key);
				if (!otherContainsKey) return false;
			}

			foreach (var entry in self)
			{
				var value = other[entry.Key];
				var valuesAreEquals = valueEquals(entry.Value, value);

				if (!valuesAreEquals) return false;
			}

			return true;
		}

		public static IDictionary<K, V> Union<K, V>(this IDictionary<K, V> self, IEnumerable<KeyValuePair<K, V>> other, Func<V, V, V> valueUnion)
		{
			var result = new Dictionary<K, V>(self);
			result.UnionWith(other, valueUnion);
			return result;
		}

		public static void UnionWith<K, V>(this IDictionary<K, V> self, IEnumerable<KeyValuePair<K, V>> other, Func<V, V, V> valueUnion)
		{
			foreach (var entry in other)
			{
				V value;

				if (self.TryGetValue(entry.Key, out value))
				{
					value = valueUnion(value, entry.Value);

					if (value != null)
					{
						self[entry.Key] = value;
					}
					else
					{
						self.Remove(entry.Key);
					}
				}
				else
				{
					self.Add(entry.Key, entry.Value);
				}
			}
		}

		public static IDictionary<K, V> Intersect<K, V>(this IDictionary<K, V> self, IEnumerable<KeyValuePair<K, V>> other, Func<V, V, V> valueIntersect)
		{
			var result = new Dictionary<K, V>();

			foreach (var entry in other)
			{
				V value;

				if (self.TryGetValue(entry.Key, out value))
				{
					value = valueIntersect(value, entry.Value);

					if (value != null)
					{
						result.Add(entry.Key, value);
					}
				}
			}

			return result;
		}

		public static void IntersectWith<K, V>(this IDictionary<K, V> self, IEnumerable<KeyValuePair<K, V>> other, Func<V, V, V> valueIntersect)
		{
			var keys = new HashSet<K>();

			foreach (var entry in other)
			{
				V value;

				if (self.TryGetValue(entry.Key, out value))
				{
					value = valueIntersect(value, entry.Value);

					if (value != null)
					{
						self[entry.Key] = value;
						keys.Add(entry.Key);
					}
					else
					{
						self.Remove(entry.Key);
					}
				}
			}

			var keysToRemove = self.Keys.Except(keys).ToArray();

			foreach (var key in keysToRemove)
			{
				self.Remove(key);
			}
		}

		//public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> elements)
		//{
		//	foreach (var element in elements)
		//	{
		//		collection.Add(element);
		//	}
		//}

		public static MapSet<K, V> ToMapSet<K, V>(this IEnumerable<V> elements, Func<V, K> keySelector)
		{
			var result = new MapSet<K, V>();

			foreach (var element in elements)
			{
				var key = keySelector(element);
				result.Add(key, element);
			}

			return result;
		}

		public static MapList<K, V> ToMapList<K, V>(this IEnumerable<V> elements, Func<V, K> keySelector)
		{
			var result = new MapList<K, V>();

			foreach (var element in elements)
			{
				var key = keySelector(element);
				result.Add(key, element);
			}

			return result;
		}

		public static Subset<T> ToSubset<T>(this T[] universe)
		{
			return new Subset<T>(universe, false);
		}

		public static Subset<T> ToEmptySubset<T>(this T[] universe)
		{
			return new Subset<T>(universe, true);
		}

        public static uint StartOffset(this IInstructionContainer block)
        {
            var instruction = block.Instructions.First();
            return instruction.Offset;
        }

        public static uint EndOffset(this IInstructionContainer block)
        {
            var instruction = block.Instructions.Last();
            return instruction.Offset;
        }

        public static ISet<IVariable> GetVariables(this IInstructionContainer block)
		{
			var result = from i in block.Instructions
						 from v in i.Variables
						 select v;

			//var result = block.Instructions.SelectMany(i => i.Variables);
			return new HashSet<IVariable>(result);
		}

		public static ISet<IVariable> GetModifiedVariables(this IInstructionContainer block)
		{
			var result = from i in block.Instructions
						 from v in i.ModifiedVariables
						 select v;

			//var result = block.Instructions.SelectMany(i => i.ModifiedVariables);
			return new HashSet<IVariable>(result);
		}

		public static ISet<IVariable> GetUsedVariables(this IInstructionContainer block)
		{
			var result = from i in block.Instructions
						 from v in i.UsedVariables
						 select v;

			//var result = block.Instructions.SelectMany(i => i.UsedVariables);
			return new HashSet<IVariable>(result);
		}

		public static ISet<IVariable> GetDefinedVariables(this IInstructionContainer block)
		{
			var result = from i in block.Instructions
						 let d = i as DefinitionInstruction
						 where d != null && d.HasResult
						 select d.Result;

			return new HashSet<IVariable>(result);
			//var result = new HashSet<IVariable>();

			//foreach (var instruction in block.Instructions)
			//{
			//    var definition = instruction as DefinitionInstruction;

			//    if (definition != null && definition.HasResult)
			//    {
			//        result.Add(definition.Result);
			//    }
			//}

			//return result;
		}

		public static ISet<CFGProtectedRegion> GetProtectedRegions(this ControlFlowGraph cfg)
		{
			var result = cfg.Regions.OfType<CFGProtectedRegion>();
			return new HashSet<CFGProtectedRegion>(result);
		}

		public static ISet<CFGLoop> GetLoops(this ControlFlowGraph cfg)
		{
			var result = cfg.Regions.OfType<CFGLoop>();
			return new HashSet<CFGLoop>(result);
		}

		public static ISet<IVariable> GetModifiedVariables(this CFGLoop loop)
		{
			var result = from n in loop.Nodes
						 from v in n.GetModifiedVariables()
						 select v;

			//var result = loop.Body.SelectMany(n => n.GetModifiedVariables());
			return new HashSet<IVariable>(result);
		}

		public static ISet<IVariable> GetVariables(this ControlFlowGraph cfg)
		{
			var result = from n in cfg.Nodes
						 from v in n.GetVariables()
						 select v;

			//var result = cfg.Nodes.SelectMany(n => n.GetVariables());
			return new HashSet<IVariable>(result);
		}

		public static ISet<CFGNode> GetExitNodes(this CFGLoop loop)
		{
			var result = from n in loop.Nodes
						 from m in n.Successors
						 where !loop.Nodes.Contains(m)
						 select n;

			//var result = loop.Body.Where(n => n.Successors.Any(m => !loop.Body.Contains(m)));
			return new HashSet<CFGNode>(result);
		}

		public static IExpression ToExpression(this IValue value)
		{
			return value as IExpression;
		}

		//public static IExpression GetValueOriginal(this IDictionary<IVariable, IExpression> equalities, IVariable variable)
		//{
		//    var result = equalities.ContainsKey(variable) ? equalities[variable] : variable;
		//    return result;
		//}
		
		public static IExpression GetValue(this IDictionary<IVariable, IExpression> equalities, IVariable variable)
		{
			IExpression result = variable;

			while (variable != null && equalities.ContainsKey(variable))
			{
				result = equalities[variable];
				variable = result as IVariable;
			}

			return result;
		}

        public static bool IsTemporal(this IVariable variable)
        {
            while (variable is DerivedVariable)
            {
                var derived = variable as DerivedVariable;
                variable = derived.Original;
            }

            var result = variable is TemporalVariable;
            return result;
        }

        public static bool IsCopy(this IInstruction instruction, out IVariable left, out IVariable right)
        {
            var result = false;
            left = null;
            right = null;

            if (instruction is LoadInstruction)
            {
                var load = instruction as LoadInstruction;

                if (load.Operand is IVariable)
                {
                    left = load.Result;
                    right = load.Operand as IVariable;
                    result = true;
                }
            }

            return result;
        }

        public static IExpression ReplaceVariables<T>(this IExpression expr, IDictionary<IVariable, T> equalities) where T : IExpression
		{
            foreach (var variable in expr.Variables)
            {
                if (variable.IsTemporal())
                {
                    var hasValue = equalities.ContainsKey(variable);

                    if (hasValue)
                    {
                        var value = equalities[variable];
                        var isUnknown = value is UnknownValue;
                        var isPhi = value is PhiExpression;
                        var isMethodCall = value is MethodCallExpression;

                        if (isUnknown || isPhi || isMethodCall)
                            continue;

                        expr = expr.Replace(variable, value);
                    }
                }
            }

            return expr;
        }

	    public static void RemoveTemporalVariables(this PointsToGraph ptg)
        {
            foreach (var variable in ptg.Roots.ToArray())
            {
                if (variable.IsTemporal())
                {
                    ptg.Remove(variable);
                }
            }
        }
        public static void RemoveDerivedVariables(this PointsToGraph ptg)
        {
            var temporals = ptg.Roots.OfType<DerivedVariable>().ToArray();

            foreach (var temporal in temporals)
            {
                ptg.Remove(temporal);
            }
        }

        #region MayAlias Analysis
        public static bool MayAlias(this PointsToGraph ptg, IVariable v1, IVariable v2)
        {
            return ptg.GetTargets(v1).Intersect(ptg.GetTargets(v2)).Any();
        }

        public static bool MayAlias(this PointsToGraph ptg, IVariable v1, IVariable v2, IFieldReference f)
        {
            var query = ptg.GetTargets(v2, f);

            return ptg.GetTargets(v1).Intersect(query).Any();
        }

        public static bool MayAlias(this PointsToGraph ptg, IVariable v1, IFieldReference f, IVariable v2)
        {
            var query = ptg.GetTargets(v1, f);
            return ptg.GetTargets(v2).Intersect(query).Any();
        }
        #endregion
        #region Reachability in PTG
        public static bool Reachable(this PointsToGraph ptg, IVariable v1, PTGNode n)
        {
            var result = false;
            ISet<PTGNode> visitedNodes = new HashSet<PTGNode>();
            Queue<PTGNode> workList = new Queue<PTGNode>();
            var nodes = ptg.GetTargets(v1, false);

            if (nodes.Contains(n) && !n.Equals(PointsToGraph.NullNode) )
                return true;

            foreach (var ptgNode in nodes)
            {
                workList.Enqueue(ptgNode);
            }
            while(workList.Any())
            {
                var ptgNode = workList.Dequeue();
                visitedNodes.Add(ptgNode);
                if (ptgNode.Equals(PointsToGraph.NullNode))
                {
                    continue;
                }
                if (ptgNode.Equals(n)) return true;
                foreach(var adjacents in ptgNode.Targets.Values)
                {
                    foreach (var adjacent in adjacents)
                    {
                        if (!visitedNodes.Contains(adjacent))
                        {
                            workList.Enqueue(adjacent);
                        }
                    }
                }
            }
            return result;
        }

        public static IEnumerable<PTGNode> ReachableNodesFromVariables(this PointsToGraph ptg)
        {
            var roots = new HashSet<PTGNode>(ptg.Roots.SelectMany(v => ptg.GetTargets(v, false)));
            roots.Add(PointsToGraph.NullNode);
            return ptg.ReachableNodes(roots);
        }
        public static IEnumerable<PTGNode> ReachableNodes(this PointsToGraph ptg, IEnumerable<PTGNode> roots, 
                                                          Predicate<Tuple<PTGNode, IFieldReference>> filter = null )
        {
            // var result = new HashSet<PTGNode>();
            ISet<PTGNode> visitedNodes = new HashSet<PTGNode>();
            Queue<PTGNode> workList = new Queue<PTGNode>();
            
            foreach (var ptgNode in roots)
            {
                workList.Enqueue(ptgNode);
            }
            while (workList.Any())
            {
                var ptgNode = workList.Dequeue();
                visitedNodes.Add(ptgNode);
                if (ptgNode.Equals(PointsToGraph.NullNode))
                {
                    continue;
                }
                foreach (var adjacents in ptgNode.Targets)
                {
                    if (filter != null)
                    {
                        var node_filter = Tuple.Create(ptgNode, adjacents.Key);
                        if (!filter(node_filter))
                            continue;
                    }
                   
                    foreach (var adjacent in adjacents.Value)
                    {
                        if (!visitedNodes.Contains(adjacent))
                        {
                            workList.Enqueue(adjacent);
                        }
                    }
                }
            }
            return visitedNodes;
        }
               

        public static bool MayReacheableFromVariable(this PointsToGraph ptg, IVariable v1, IVariable v2)
        {
            var result = ptg.GetTargets(v2, false).Any(n => ptg.Reachable(v1, n));
            return result;
        }
        public static  ISet<IVariable> GetAliases(this PointsToGraph ptg, IVariable v)
        {
            var res = new HashSet<IVariable>() { v };
            foreach (var ptgNode in ptg.GetTargets(v, false)) // GetPtgNodes(v))
            {
                if (ptgNode != PointsToGraph.NullNode)
                {
                    res.UnionWith(ptgNode.Variables);
                }
            }
            return res;
        }


        #endregion

        public static bool IsPure(this IMethodReference method)
		{
			var result = method.Attributes.Any(a => a.Type.Equals(PlatformTypes.PureAttribute));

			if (!result)
			{
				result = method.Name == "get_Count" && TypeHelper.IsContainer(method.ContainingType);
			}

			return result;
		}

        public static bool IsConstructorCall(this Instruction ins)
        {
            if(ins is MethodCallInstruction)
            {
                var call = ins as MethodCallInstruction;
                if (call.Method.Name == ".ctor")
                {
                    return true;
                }
            }
            return false;
        }
        public static bool IsConstructor(this IMethodReference method)
        {
            if (method.Name == ".ctor")
            {
                return true;
            }
            return false;
        }

        public static void Inline(this MethodBody callerBody, MethodCallInstruction methodCall, MethodBody calleeBody)
        {
            // TODO: Fix local variables (and parameters) name clashing

            var index = callerBody.Instructions.IndexOf(methodCall);
            callerBody.Instructions.RemoveAt(index);

            IInstruction nextInstruction = null;

            if (callerBody.Instructions.Count > index)
            {
                // The caller method has more instructions after the method call
                nextInstruction = callerBody.Instructions[index];
            }

            for (var i = 0; i < calleeBody.Parameters.Count; ++i)
            {
                var parameter = calleeBody.Parameters[i];
                var argument = methodCall.Arguments[i];
                var copy = new LoadInstruction(methodCall.Offset, parameter, argument);

                copy.Label = string.Format("{0}_{1}", methodCall.Label, copy.Label);
                callerBody.Instructions.Insert(index, copy);
                index++;
            }

            var lastCalleeInstructionIndex = calleeBody.Instructions.Count - 1;

            for (var i = 0; i < calleeBody.Instructions.Count; ++i)
            {
                var instruction = calleeBody.Instructions[i];

                if (instruction is ReturnInstruction)
                {
                    var ret = instruction as ReturnInstruction;

                    if (ret.HasOperand && methodCall.HasResult)
                    {
                        // Copy the return value of the callee to the result variable of the method call
                        var copy = new LoadInstruction(ret.Offset, methodCall.Result, ret.Operand);

                        copy.Label = string.Format("{0}_{1}", methodCall.Label, copy.Label);
                        callerBody.Instructions.Insert(index, copy);
                        index++;
                    }

                    if (nextInstruction != null && i < lastCalleeInstructionIndex)
                    {
                        // Jump to the instruction after the method call
                        var branch = new UnconditionalBranchInstruction(ret.Offset, nextInstruction.Offset);

                        branch.Label = string.Format("{0}_{1}", methodCall.Label, branch.Label);
                        callerBody.Instructions.Insert(index, branch);
                        index++;
                    }
                }
                else
                {
                    // TODO: Fix! We should clone the instruction
                    // so the original is not modified
                    // and calleeBody remain intacted

                    if (instruction is BranchInstruction)
                    {
                        var branch = instruction as BranchInstruction;
                        branch.Target = string.Format("{0}_{1}", methodCall.Label, branch.Target);
                    }
                    else if (instruction is SwitchInstruction)
                    {
                        var branch = instruction as SwitchInstruction;

                        for (var j = 0; j < branch.Targets.Count; ++j)
                        {
                            var target = branch.Targets[j];
                            branch.Targets[j] = string.Format("{0}_{1}", methodCall.Label, target);
                        }
                    }

                    instruction.Label = string.Format("{0}_{1}", methodCall.Label, instruction.Label);
                    callerBody.Instructions.Insert(index, instruction);
                    index++;
                }
            }
        }
        // From Zvonimir to get the full name with all the containing types
        public static string FullPathName(this ITypeDefinition type)
        {
            if (type.ContainingType == null) return type.ContainingNamespace.FullName + "." + type.Name;
            return type.ContainingType.FullPathName() + "." + type.Name;
        }
        public static bool IsReferenceType(this IType type)
        {
            return type.TypeKind == TypeKind.ReferenceType;
        }

        public static bool IsClassOrStruct(this IType type)
        {
            string[] primitiveTypes = { "Void", "Double", "Integer", "Boolean", "Char", "Int32", "Int16", "Int64" };
            var isRefType = type.TypeKind == TypeKind.ReferenceType;
            if(!isRefType)
            {
                var basicType = type as IBasicType;
                if (basicType != null)
                {
                    if (basicType.ResolvedType != null && ((basicType.ResolvedType is ClassDefinition) || (basicType.ResolvedType is StructDefinition)))
                    {
                        if(!primitiveTypes.Contains(basicType.ResolvedType.Name))
                            return true;
                    }
                }
            }
            return isRefType;
        }

        public static bool IsScalar(this IType type)
        {
            var isValueType = type.TypeKind == TypeKind.ValueType;
            if (isValueType)
            {
                var basicType = type as IBasicType;
                if (basicType.ResolvedType != null && (basicType.ResolvedType is StructDefinition))
                {
                    return false;
                }
            }
            return isValueType;
        }

        public static bool IsValueType(this IType type)
        {
            return type.TypeKind == TypeKind.ValueType;
        }

        public static bool IsDelegateType(this IType type)
        {
            var basicType = type as IBasicType;
            if (basicType != null && basicType.ResolvedType is ClassDefinition)
            {
                if ((basicType.ResolvedType as ClassDefinition).IsDelegate)
                {
                    return true;
                }
            }
            return false;
        }


        public static IMethodReference FindMethodImplementation(this Host host, IBasicType receiverType, IMethodReference method)
        {
            var result = method;


            while (receiverType != null)
            {
                var receiverTypeDef = receiverType.ResolvedType as ClassDefinition;
                if (receiverTypeDef == null) break;

                var matchingMethod = receiverTypeDef.Methods.SingleOrDefault(m => m.MatchSignature(method));

                if (matchingMethod != null)
                {
                    result = matchingMethod;
                    break;
                }
                else
                {
                    receiverType = receiverTypeDef.Base;
                }

            }

            return result;
        }
       public static Backend.Model.ControlFlowGraph DoAnalysisPhases(this MethodDefinition method, Host host, IEnumerable<IMethodReference> methodsToTryToInline = null)
        {
            var disassembler = new Disassembler(method);
            var methodBody = disassembler.Execute();
            method.Body = methodBody;
            
            if(methodsToTryToInline != null)
            {
                DoInlining(method, host, methodBody, methodsToTryToInline);
            }

            var cfAnalysis = new ControlFlowAnalysis(method.Body);
            var cfg = cfAnalysis.GenerateExceptionalControlFlow();
            // var cfg = cfAnalysis.GenerateNormalControlFlow();

            var domAnalysis = new DominanceAnalysis(cfg);
            domAnalysis.Analyze();
            domAnalysis.GenerateDominanceTree();

            var loopAnalysis = new NaturalLoopAnalysis(cfg);
            loopAnalysis.Analyze();

            var domFrontierAnalysis = new DominanceFrontierAnalysis(cfg);
            domFrontierAnalysis.Analyze();

            var splitter = new WebAnalysis(cfg);
            splitter.Analyze();
            splitter.Transform();

            methodBody.UpdateVariables();


            var analysis = new TypeInferenceAnalysis(cfg);
            analysis.Analyze();

            var copyProgapagtion = new ForwardCopyPropagationAnalysis(cfg);
            copyProgapagtion.Analyze();
            copyProgapagtion.Transform(methodBody);

            //var backwardCopyProgapagtion = new BackwardCopyPropagationAnalysis(cfg);
            //backwardCopyProgapagtion.Analyze();
            //backwardCopyProgapagtion.Transform(methodBody);

            var liveVariables = new LiveVariablesAnalysis(cfg);
            var resultLiveVar = liveVariables.Analyze();


            var ssa = new StaticSingleAssignment(methodBody, cfg);
            ssa.Transform();
            ssa.Prune(liveVariables);
            methodBody.UpdateVariables();

            method.Body = methodBody;

             // var dgml = DGMLSerializer.Serialize(cfg);
            return cfg;
        }

        public static IEnumerable<IMethodReference> GetMethodsInvoked(this MethodDefinition method)
        {
            var disassembler = new Disassembler(method);
            var methodBody = disassembler.Execute();
            return methodBody.Instructions.OfType<MethodCallInstruction>().Select(ins => ins.Method);
        }

        private static void DoInlining(MethodDefinition method, Host host, MethodBody methodBody, IEnumerable<IMethodReference> methodsToTryToInline = null)
        {
            if (methodsToTryToInline == null)
                methodsToTryToInline = new HashSet<IMethodReference>();

            var methodCalls = methodBody.Instructions.OfType<MethodCallInstruction>().Where(ins => methodsToTryToInline.Contains(ins.Method)).ToList();
            foreach (var methodCall in methodCalls)
            {
                var calleeM = host.ResolveReference(methodCall.Method);
                var callee = calleeM as MethodDefinition;
                if (callee != null)
                {
                    // var calleeCFG = DoAnalysisPhases(callee, host);
                    var disassemblerCallee = new Disassembler(callee);
                    var methodBodyCallee = disassemblerCallee.Execute();
                    callee.Body = methodBodyCallee;
                    methodBody.Inline(methodCall, callee.Body);
                }
            }

            methodBody.UpdateVariables();

            method.Body = methodBody;
        }

    }
}
