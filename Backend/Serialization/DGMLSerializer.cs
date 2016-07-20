﻿// Copyright (c) Edgardo Zoppi.  All Rights Reserved.  Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Backend.Analyses;
using Model.Types;
using Model;
using Backend.Model;

namespace Backend.Serialization
{
	public static class DGMLSerializer
	{
		#region Control-Flow Graph

		public static string Serialize(ControlFlowGraph cfg)
		{
			using (var stringWriter = new StringWriter())
			using (var xmlWriter = new XmlTextWriter(stringWriter))
			{
				xmlWriter.Formatting = Formatting.Indented;
				xmlWriter.WriteStartElement("DirectedGraph");
				xmlWriter.WriteAttributeString("xmlns", "http://schemas.microsoft.com/vs/2009/dgml");
				xmlWriter.WriteStartElement("Nodes");

				foreach (var node in cfg.Nodes)
				{
					var nodeId = Convert.ToString(node.Id);
					var label = DGMLSerializer.Serialize(node);

					xmlWriter.WriteStartElement("Node");
					xmlWriter.WriteAttributeString("Id", nodeId);
					xmlWriter.WriteAttributeString("Label", label);

					if (node.Kind == CFGNodeKind.Entry ||
						node.Kind == CFGNodeKind.Exit)
					{
						xmlWriter.WriteAttributeString("Background", "Yellow");
					}

					xmlWriter.WriteEndElement();
				}

				xmlWriter.WriteEndElement();
				xmlWriter.WriteStartElement("Links");

				foreach (var node in cfg.Nodes)
				{
					var sourceId = Convert.ToString(node.Id);

					foreach (var successor in node.Successors)
					{
						var targetId = Convert.ToString(successor.Id);

						xmlWriter.WriteStartElement("Link");
						xmlWriter.WriteAttributeString("Source", sourceId);
						xmlWriter.WriteAttributeString("Target", targetId);
						xmlWriter.WriteEndElement();
					}
				}

				xmlWriter.WriteEndElement();
				xmlWriter.WriteStartElement("Styles");
				xmlWriter.WriteStartElement("Style");
				xmlWriter.WriteAttributeString("TargetType", "Node");

				xmlWriter.WriteStartElement("Setter");
				xmlWriter.WriteAttributeString("Property", "FontFamily");
				xmlWriter.WriteAttributeString("Value", "Consolas");
				xmlWriter.WriteEndElement();

				xmlWriter.WriteStartElement("Setter");
				xmlWriter.WriteAttributeString("Property", "NodeRadius");
				xmlWriter.WriteAttributeString("Value", "5");
				xmlWriter.WriteEndElement();

				xmlWriter.WriteStartElement("Setter");
				xmlWriter.WriteAttributeString("Property", "MinWidth");
				xmlWriter.WriteAttributeString("Value", "0");
				xmlWriter.WriteEndElement();

				xmlWriter.WriteEndElement();
				xmlWriter.WriteEndElement();
				xmlWriter.WriteEndElement();
				xmlWriter.Flush();
				return stringWriter.ToString();
			}
		}

		private static string Serialize(CFGNode node)
		{
			string result;

			switch (node.Kind)
			{
				case CFGNodeKind.Entry: result = "entry"; break;
				case CFGNodeKind.Exit: result = "exit"; break;
				default: result = node.Id+Environment.NewLine+ string.Join(Environment.NewLine, node.Instructions); break;
			}

			return result;
		}

		#endregion

		#region Points-To Graph

		public static string Serialize(PointsToGraph ptg)
		{
			using (var stringWriter = new StringWriter())
			using (var xmlWriter = new XmlTextWriter(stringWriter))
			{
				xmlWriter.Formatting = Formatting.Indented;
				xmlWriter.WriteStartElement("DirectedGraph");
				xmlWriter.WriteAttributeString("xmlns", "http://schemas.microsoft.com/vs/2009/dgml");
				xmlWriter.WriteStartElement("Nodes");

				foreach (var variable in ptg.Variables)
				{
					var label = variable.Name;

					xmlWriter.WriteStartElement("Node");
					xmlWriter.WriteAttributeString("Id", label);
					xmlWriter.WriteAttributeString("Label", label);
					xmlWriter.WriteAttributeString("Shape", "None");
					xmlWriter.WriteEndElement();
				}

				foreach (var node in ptg.Nodes)
				{
					var nodeId = Convert.ToString(node.Id);
					var label = DGMLSerializer.Serialize(node);

					xmlWriter.WriteStartElement("Node");
					xmlWriter.WriteAttributeString("Id", nodeId);
					xmlWriter.WriteAttributeString("Label", label);

					if (node.Kind == PTGNodeKind.Null)
					{
						xmlWriter.WriteAttributeString("Background", "Yellow");
					}
					else if (node.Kind == PTGNodeKind.Unknown)
					{
						xmlWriter.WriteAttributeString("Background", "#FFB445");
						xmlWriter.WriteAttributeString("StrokeDashArray", "6,6");
					}

					xmlWriter.WriteEndElement();
				}

				xmlWriter.WriteEndElement();
				xmlWriter.WriteStartElement("Links");

				foreach (var node in ptg.Nodes)
				{
					var targetId = Convert.ToString(node.Id);

					foreach (var variable in node.Variables)
					{
						var sourceId = variable.Name;

						xmlWriter.WriteStartElement("Link");
						xmlWriter.WriteAttributeString("Source", sourceId);
						xmlWriter.WriteAttributeString("Target", targetId);
						xmlWriter.WriteEndElement();
					}

					var fieldsBySource = from e in node.Sources
										 from s in e.Value
										 group e.Key by s into g
										 select g;

					foreach (var g in fieldsBySource)
					{
						var sourceId = Convert.ToString(g.Key.Id);
						var label = DGMLSerializer.GetLabel(g);

						xmlWriter.WriteStartElement("Link");
						xmlWriter.WriteAttributeString("Source", sourceId);
						xmlWriter.WriteAttributeString("Target", targetId);
						xmlWriter.WriteAttributeString("Label", label);
						xmlWriter.WriteEndElement();
					}
				}

				xmlWriter.WriteEndElement();
				xmlWriter.WriteStartElement("Styles");
				xmlWriter.WriteStartElement("Style");
				xmlWriter.WriteAttributeString("TargetType", "Node");

				xmlWriter.WriteStartElement("Setter");
				xmlWriter.WriteAttributeString("Property", "FontFamily");
				xmlWriter.WriteAttributeString("Value", "Consolas");
				xmlWriter.WriteEndElement();

				xmlWriter.WriteStartElement("Setter");
				xmlWriter.WriteAttributeString("Property", "NodeRadius");
				xmlWriter.WriteAttributeString("Value", "5");
				xmlWriter.WriteEndElement();

				xmlWriter.WriteStartElement("Setter");
				xmlWriter.WriteAttributeString("Property", "MinWidth");
				xmlWriter.WriteAttributeString("Value", "0");
				xmlWriter.WriteEndElement();

				xmlWriter.WriteEndElement();
				xmlWriter.WriteEndElement();
				xmlWriter.WriteEndElement();
				xmlWriter.Flush();
				return stringWriter.ToString();
			}
		}

		private static string Serialize(PTGNode node)
		{
			string result;

			switch (node.Kind)
			{
				case PTGNodeKind.Null: result = "null"; break;
				default: result = String.Format("{0:X4}:{1}",node.Offset, node.Type); break;
                     
			}

			return result;
		}

		#endregion

		#region Type Graph
		
		public static string Serialize(Host host, ITypeDefinition type)
		{
			var types = new ITypeDefinition[] { type };
			return DGMLSerializer.Serialize(host, types);
		}

		public static string Serialize(Host host, Assembly assembly)
		{
			var types = assembly.RootNamespace.GetAllTypes();
			return DGMLSerializer.Serialize(host, types);
		}

		private static string Serialize(Host host, IEnumerable<ITypeDefinition> types)
		{
			using (var stringWriter = new StringWriter())
			using (var xmlWriter = new XmlTextWriter(stringWriter))
			{
				var allReferencedTypes = new Dictionary<BasicType, int>();
				var allDefinedTypes = new Dictionary<ITypeDefinition, int>();
				var visitedTypes = new HashSet<ITypeDefinition>();
				var newTypes = new HashSet<ITypeDefinition>();

				xmlWriter.Formatting = Formatting.Indented;
				xmlWriter.WriteStartElement("DirectedGraph");
				xmlWriter.WriteAttributeString("xmlns", "http://schemas.microsoft.com/vs/2009/dgml");
				xmlWriter.WriteStartElement("Links");

				foreach (var type in types)
				{
					allDefinedTypes.Add(type, allDefinedTypes.Count);
					newTypes.Add(type);
				}

				while (newTypes.Count > 0)
				{
					var type = newTypes.First();
					newTypes.Remove(type);
					visitedTypes.Add(type);

					var typeId = allDefinedTypes[type];
					var sourceId = string.Format("d{0}", typeId);
					var targetId = string.Empty;

					var fieldsByType = from m in type.Members
									   let f = m as FieldDefinition
									   where f != null && f.Type is BasicType
									   let ftype = f.Type as BasicType
									   group f by ftype into g
									   select g;

					foreach (var g in fieldsByType)
					{
						var fieldTypeRef = g.Key;
						var fieldTypeDef = host.ResolveReference(g.Key);

						if (fieldTypeDef == null)
						{
							if (!allReferencedTypes.ContainsKey(fieldTypeRef))
							{
								allReferencedTypes.Add(fieldTypeRef, allReferencedTypes.Count);
							}

							typeId = allReferencedTypes[fieldTypeRef];
							targetId = string.Format("r{0}", typeId);
						}
						else
						{
							if (!allDefinedTypes.ContainsKey(fieldTypeDef))
							{
								allDefinedTypes.Add(fieldTypeDef, allDefinedTypes.Count);
							}

							typeId = allDefinedTypes[fieldTypeDef];
							targetId = string.Format("d{0}", typeId);

							if (!visitedTypes.Contains(fieldTypeDef))
							{
								newTypes.Add(fieldTypeDef);
							}
						}

						var label = DGMLSerializer.GetLabel(g);

						xmlWriter.WriteStartElement("Link");
						xmlWriter.WriteAttributeString("Source", sourceId);
						xmlWriter.WriteAttributeString("Target", targetId);
						xmlWriter.WriteAttributeString("Label", label);
						xmlWriter.WriteEndElement();
					}
				}

				xmlWriter.WriteEndElement();
				xmlWriter.WriteStartElement("Nodes");

				foreach (var entry in allReferencedTypes)
				{
					var typeId = string.Format("r{0}", entry.Value);
					var label = entry.Key.FullName;

					xmlWriter.WriteStartElement("Node");
					xmlWriter.WriteAttributeString("Id", typeId);
					xmlWriter.WriteAttributeString("Label", label);
					xmlWriter.WriteEndElement();
				}

				foreach (var entry in allDefinedTypes)
				{
					var typeId = string.Format("d{0}", entry.Value);
					var label = entry.Key.FullName;

					xmlWriter.WriteStartElement("Node");
					xmlWriter.WriteAttributeString("Id", typeId);
					xmlWriter.WriteAttributeString("Label", label);
					xmlWriter.WriteEndElement();
				}

				xmlWriter.WriteEndElement();
				xmlWriter.WriteStartElement("Styles");
				xmlWriter.WriteStartElement("Style");
				xmlWriter.WriteAttributeString("TargetType", "Node");

				xmlWriter.WriteStartElement("Setter");
				xmlWriter.WriteAttributeString("Property", "FontFamily");
				xmlWriter.WriteAttributeString("Value", "Consolas");
				xmlWriter.WriteEndElement();

				xmlWriter.WriteStartElement("Setter");
				xmlWriter.WriteAttributeString("Property", "NodeRadius");
				xmlWriter.WriteAttributeString("Value", "5");
				xmlWriter.WriteEndElement();

				xmlWriter.WriteStartElement("Setter");
				xmlWriter.WriteAttributeString("Property", "MinWidth");
				xmlWriter.WriteAttributeString("Value", "0");
				xmlWriter.WriteEndElement();

				xmlWriter.WriteEndElement();
				xmlWriter.WriteEndElement();
				xmlWriter.WriteEndElement();
				xmlWriter.Flush();
				return stringWriter.ToString();
			}
		}

        #endregion


        #region Graph

        public static string Serialize<N,F>(Graph<N,F> g)
        {
            using (var stringWriter = new StringWriter())
            using (var xmlWriter = new XmlTextWriter(stringWriter))
            {
                xmlWriter.Formatting = Formatting.Indented;
                xmlWriter.WriteStartElement("DirectedGraph");
                xmlWriter.WriteAttributeString("xmlns", "http://schemas.microsoft.com/vs/2009/dgml");
                xmlWriter.WriteStartElement("Nodes");

                foreach (var node in g.Nodes)
                {
                    var label = node.ToString();

                    xmlWriter.WriteStartElement("Node");
                    xmlWriter.WriteAttributeString("Label", label);
                    xmlWriter.WriteAttributeString("Shape", "None");
                    xmlWriter.WriteEndElement();
                }

                xmlWriter.WriteEndElement();
                xmlWriter.WriteStartElement("Links");

                foreach (var node in g.Nodes)
                {
                    var sourceId = node.ToString();

                    foreach (var suc in node.Successors)
                    {
                        var targetId = suc.ToString();

                        xmlWriter.WriteStartElement("Link");
                        xmlWriter.WriteAttributeString("Source", sourceId);
                        xmlWriter.WriteAttributeString("Target", targetId);
                        xmlWriter.WriteEndElement();
                    }
                }

                xmlWriter.WriteEndElement();
                xmlWriter.WriteStartElement("Styles");
                xmlWriter.WriteStartElement("Style");
                xmlWriter.WriteAttributeString("TargetType", "Node");

                xmlWriter.WriteStartElement("Setter");
                xmlWriter.WriteAttributeString("Property", "FontFamily");
                xmlWriter.WriteAttributeString("Value", "Consolas");
                xmlWriter.WriteEndElement();

                xmlWriter.WriteStartElement("Setter");
                xmlWriter.WriteAttributeString("Property", "NodeRadius");
                xmlWriter.WriteAttributeString("Value", "5");
                xmlWriter.WriteEndElement();

                xmlWriter.WriteStartElement("Setter");
                xmlWriter.WriteAttributeString("Property", "MinWidth");
                xmlWriter.WriteAttributeString("Value", "0");
                xmlWriter.WriteEndElement();

                xmlWriter.WriteEndElement();
                xmlWriter.WriteEndElement();
                xmlWriter.WriteEndElement();
                xmlWriter.Flush();
                return stringWriter.ToString();
            }
        }

        public static string Serialize(InstructionDependencyGraph graph)
        {
            using (var stringWriter = new StringWriter())
            using (var xmlWriter = new XmlTextWriter(stringWriter))
            {
                xmlWriter.Formatting = Formatting.Indented;
                xmlWriter.WriteStartElement("DirectedGraph");
                xmlWriter.WriteAttributeString("xmlns", "http://schemas.microsoft.com/vs/2009/dgml");
                xmlWriter.WriteStartElement("Nodes");

                foreach (var node in graph.Nodes)
                {
                    var nodeId = string.Format("L_{0:X4}", node);  // node.ToString();

                    var label = graph.Instruction(node);

                    xmlWriter.WriteStartElement("Node");
                    xmlWriter.WriteAttributeString("Id", nodeId);

                    xmlWriter.WriteAttributeString("Label", label);
                    xmlWriter.WriteAttributeString("Shape", "None");
                    xmlWriter.WriteEndElement();
                }

                xmlWriter.WriteEndElement();
                xmlWriter.WriteStartElement("Links");

                foreach (var node in graph.Nodes)
                {
                    var sourceId = string.Format("L_{0:X4}", node);   // node.ToString();

                    foreach (var suc in graph.Successors(node))
                    {
                        var targetId = string.Format("L_{0:X4}", suc);  // suc.ToString();

                        xmlWriter.WriteStartElement("Link");
                        xmlWriter.WriteAttributeString("Source", sourceId);
                        xmlWriter.WriteAttributeString("Target", targetId);
                        xmlWriter.WriteEndElement();
                    }
                }

                xmlWriter.WriteEndElement();
                xmlWriter.WriteStartElement("Styles");
                xmlWriter.WriteStartElement("Style");
                xmlWriter.WriteAttributeString("TargetType", "Node");

                xmlWriter.WriteStartElement("Setter");
                xmlWriter.WriteAttributeString("Property", "FontFamily");
                xmlWriter.WriteAttributeString("Value", "Consolas");
                xmlWriter.WriteEndElement();

                xmlWriter.WriteStartElement("Setter");
                xmlWriter.WriteAttributeString("Property", "NodeRadius");
                xmlWriter.WriteAttributeString("Value", "5");
                xmlWriter.WriteEndElement();

                xmlWriter.WriteStartElement("Setter");
                xmlWriter.WriteAttributeString("Property", "MinWidth");
                xmlWriter.WriteAttributeString("Value", "0");
                xmlWriter.WriteEndElement();

                xmlWriter.WriteEndElement();
                xmlWriter.WriteEndElement();
                xmlWriter.WriteEndElement();
                xmlWriter.Flush();
                return stringWriter.ToString();
            }
        }

        #endregion

        #region Private Methods

        private static string GetLabel(IEnumerable<IFieldReference> fields)
		{
			var result = new StringBuilder();

			foreach (var field in fields)
			{
				result.Append(field.Name);
				result.AppendLine();
			}

			result.Remove(result.Length - 2, 2);
			return result.ToString();
		}

		private static string GetLabel(IEnumerable<FieldDefinition> fields)
		{
			var result = new StringBuilder();

			foreach (var field in fields)
			{
				result.Append(field.Name);
				result.AppendLine();
			}

			result.Remove(result.Length - 2, 2);
			return result.ToString();
		}

		#endregion
	}
}
