﻿// Copyright (c) Edgardo Zoppi.  All Rights Reserved.  Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Model.Types;
using Model.ThreeAddressCode.Expressions;

namespace Model.ThreeAddressCode.Values
{
	public interface IVariableContainer
	{
		ISet<IVariable> Variables { get; }
		void Replace(IVariable oldvar, IVariable newvar);
	}

	public interface IValue : IVariableContainer, IExpressible
	{
		IType Type { get; }
	}

	public interface IAssignableValue : IValue
	{
	}

	public interface IReferenceable : IVariableContainer, IExpression
	{
	}

	public interface IFunctionReference : IVariableContainer, IExpression
	{
		IMethodReference Method { get; set; }
	}

	public class StaticMethodReference : IFunctionReference
	{
		public IMethodReference Method { get; set; }

		public StaticMethodReference(IMethodReference method)
		{
			this.Method = method;
		}

		public IType Type
		{
			get { return new FunctionPointerType(this.Method); }
		}

		ISet<IVariable> IVariableContainer.Variables
		{
			get { return new HashSet<IVariable>(); }
		}

		void IVariableContainer.Replace(IVariable oldvar, IVariable newvar)
		{
		}

		IExpression IExpression.Replace(IExpression oldexpr, IExpression newexpr)
		{
			return this;
		}

		IExpression IExpressible.ToExpression()
		{
			return this;
		}

		public override bool Equals(object obj)
		{
			if (object.ReferenceEquals(this, obj)) return true;
			var other = obj as StaticMethodReference;

			return other != null &&
				this.Method.Equals(other.Method);
		}

		public override int GetHashCode()
		{
			return this.Method.GetHashCode();
		}

		public override string ToString()
		{
			return string.Format("&{0}::{1}", this.Method.ContainingType, this.Method);
		}
	}

	public class VirtualMethodReference : IFunctionReference
	{
		public IVariable Instance { get; set; }
		public IMethodReference Method { get; set; }

		public VirtualMethodReference(IVariable instance, IMethodReference method)
		{
			this.Instance = instance;
			this.Method = method;
		}

		public IType Type
		{
			get { return new FunctionPointerType(this.Method); }
		}

		public ISet<IVariable> Variables
		{
			get { return new HashSet<IVariable>() { this.Instance }; }
		}

		public void Replace(IVariable oldvar, IVariable newvar)
		{
			if (this.Instance.Equals(oldvar)) this.Instance = newvar;
		}

		IExpression IExpression.Replace(IExpression oldexpr, IExpression newexpr)
		{
			var result = this;

			if (oldexpr is IVariable && newexpr is IVariable)
			{
				var instance = (this.Instance as IExpression).Replace(oldexpr, newexpr) as IVariable;
				result = new VirtualMethodReference(instance, this.Method);
			}

			return result;
		}

		IExpression IExpressible.ToExpression()
		{
			return this;
		}

		public override bool Equals(object obj)
		{
			if (object.ReferenceEquals(this, obj)) return true;
			var other = obj as VirtualMethodReference;

			return other != null &&
				this.Instance.Equals(other.Instance) &&
				this.Method.Equals(other.Method);
		}

		public override int GetHashCode()
		{
			return this.Instance.GetHashCode() ^
				this.Method.GetHashCode();
		}

		public override string ToString()
		{
			return string.Format("&{0}::{1}({2})", this.Method.ContainingType, this.Method, this.Instance);
		}
	}

	public interface IInmediateValue : IValue, IExpression
	{
		new IType Type { get; set; }
	}

	public sealed class UnknownValue : IInmediateValue
	{
		private static UnknownValue value;
		public IType Type { get; set; }

		private UnknownValue() { }

		public static UnknownValue Value
		{
			get
			{
				if (value == null) value = new UnknownValue();
				return value;
			}
		}

		ISet<IVariable> IVariableContainer.Variables
		{
			get { return new HashSet<IVariable>(); }
		}

		void IVariableContainer.Replace(IVariable oldvar, IVariable newvar)
		{
		}

		IExpression IExpression.Replace(IExpression oldexpr, IExpression newexpr)
		{
			return this;
		}

		IExpression IExpressible.ToExpression()
		{
			return this;
		}

		public override string ToString()
		{
			return "UNK";
		}
	}

	public class Constant : IInmediateValue
	{
		public object Value { get; set; }
		public IType Type { get; set; }

		public Constant(object value)
		{
			this.Value = value;
		}

		ISet<IVariable> IVariableContainer.Variables
		{
			get { return new HashSet<IVariable>(); }
		}

		void IVariableContainer.Replace(IVariable oldvar, IVariable newvar)
		{
		}

		IExpression IExpression.Replace(IExpression oldexpr, IExpression newexpr)
		{
			if (this.Equals(oldexpr)) return newexpr;
			return this;
		}

		IExpression IExpressible.ToExpression()
		{
			return this;
		}

		public override bool Equals(object obj)
		{
			if (object.ReferenceEquals(this, obj)) return true;
			var other = obj as Constant;

			return other != null &&
				this.Value.Equals(other.Value);
		}

		public override int GetHashCode()
		{
			return this.Value.GetHashCode();
		}

		public override string ToString()
		{
			string result;

			if (this.Value == null)
			{
				result = "null";
			}
			else if (this.Value is string)
			{
				result = string.Format("\"{0}\"", this.Value);
			}
			else if (this.Value is char)
			{
				result = string.Format("'{0}'", this.Value);
			}
			else
			{
				result = Convert.ToString(this.Value);

				if (this.Value is bool)
				{
					result = result.ToLower();
				}
			}

			return result;
		}
	}

	public interface IVariable : IInmediateValue, IReferenceable
	{
		string Name { get; }
		bool IsParameter { get; }
	}

	public class LocalVariable : IVariable
	{
		public string Name { get; set; }
		public IType Type { get; set; }
		public bool IsParameter { get; set; }

		public LocalVariable(string name, bool isParameter)
		{
			this.Name = name;
			this.IsParameter = isParameter;
		}

		public LocalVariable(string name)
		{
			this.Name = name;
		}

		ISet<IVariable> IVariableContainer.Variables
		{
			get { return new HashSet<IVariable>() { this }; }
		}

		void IVariableContainer.Replace(IVariable oldvar, IVariable newvar)
		{
		}

		IExpression IExpression.Replace(IExpression oldexpr, IExpression newexpr)
		{
			if (this.Equals(oldexpr)) return newexpr;
			return this;
		}

		IExpression IExpressible.ToExpression()
		{
			return this;
		}

		public override bool Equals(object obj)
		{
			if (object.ReferenceEquals(this, obj)) return true;
			var other = obj as IVariable;

			return other != null &&
				this.Name.Equals(other.Name);
		}

		public override int GetHashCode()
		{
			return this.Name.GetHashCode();
		}

		public override string ToString()
		{
			return this.Name;
		}
	}

	public class TemporalVariable : IVariable
	{
		private string name;
        private string _fullName;

        public IType Type { get; set; }
		public uint Index { get; set; }

		public TemporalVariable(string name, uint index)
		{
			this.name = name;
			this.Index = index;
		}

		public TemporalVariable(uint index)
			: this("$t", index)
		{
		}

		public string Name
		{
			get {
                if (_fullName == null)
                {
                    _fullName = string.Format("{0}{1}", this.name, this.Index);
                }
                return _fullName;
            }
		}

		public bool IsParameter
		{
			get { return false; }
		}

		ISet<IVariable> IVariableContainer.Variables
		{
			get { return new HashSet<IVariable>() { this }; }
		}

		void IVariableContainer.Replace(IVariable oldvar, IVariable newvar)
		{
		}

		IExpression IExpression.Replace(IExpression oldexpr, IExpression newexpr)
		{
			if (this.Equals(oldexpr)) return newexpr;
			return this;
		}

		IExpression IExpressible.ToExpression()
		{
			return this;
		}

		public override bool Equals(object obj)
		{
			if (object.ReferenceEquals(this, obj)) return true;
			var other = obj as IVariable;

			return other != null &&
				this.Name.Equals(other.Name);
		}

		public override int GetHashCode()
		{
			return this.Name.GetHashCode();
		}

		public override string ToString()
		{
			return this.Name;
		}
	}

	public class DerivedVariable : IVariable
	{
		public IVariable Original { get; set; }
		public uint Index { get; set; }

        private string _name;

		public DerivedVariable(IVariable original, uint index)
		{
			this.Original = original;
			this.Index = index;
		}

		public string Name
		{
			get
			{
                if (_name == null)
                {
                    var result = this.Original.Name;

                    if (this.Index > 0)
                    {
                        result = string.Format("{0}_{1}", result, this.Index);
                    }
                    _name = result;
                }
				return _name;
			}
		}

		public bool IsParameter
		{
			get { return this.Original.IsParameter && this.Index == 0; }
		}

		public IType Type
		{
			get { return this.Original.Type; }
			set { this.Original.Type = value; }
		}

		ISet<IVariable> IVariableContainer.Variables
		{
			get { return new HashSet<IVariable>() { this }; }
		}

		void IVariableContainer.Replace(IVariable oldvar, IVariable newvar)
		{
		}

		IExpression IExpression.Replace(IExpression oldexpr, IExpression newexpr)
		{
			if (this.Equals(oldexpr)) return newexpr;
			return this;
		}

		IExpression IExpressible.ToExpression()
		{
			return this;
		}

		public override bool Equals(object obj)
		{
			if (object.ReferenceEquals(this, obj)) return true;
			var other = obj as IVariable;

			return other != null &&
				this.Name.Equals(other.Name);
		}

		public override int GetHashCode()
		{
			return this.Name.GetHashCode();
		}

		public override string ToString()
		{
			return this.Name;
		}
	}

	public interface IFieldAccess : IExpression
	{
		string Name { get; }
		string FieldName { get; }
	}

	public class StaticFieldAccess : IFieldAccess, IAssignableValue, IReferenceable
	{
		public IFieldReference Field { get; set; }

		public StaticFieldAccess(IFieldReference field)
		{
			this.Field = field;
		}

		public string FieldName
		{
			get { return this.Field.Name; }
		}

		public string Name
		{
			get { return string.Format("{0}::{1}", this.Field.ContainingType, this.FieldName); }
		}

		public IType Type
		{
			get { return this.Field.Type; }
		}

		public ISet<IVariable> Variables
		{
			get { return new HashSet<IVariable>(); }
		}

		public void Replace(IVariable oldvar, IVariable newvar)
		{
		}

		IExpression IExpression.Replace(IExpression oldexpr, IExpression newexpr)
		{
			if (this.Equals(oldexpr)) return newexpr;
			return this;
		}

		IExpression IExpressible.ToExpression()
		{
			return this;
		}

		public override bool Equals(object obj)
		{
			if (object.ReferenceEquals(this, obj)) return true;
			var other = obj as StaticFieldAccess;

			return other != null &&
				this.Name.Equals(other.Name);
		}

		public override int GetHashCode()
		{
			return this.Name.GetHashCode();
		}

		public override string ToString()
		{
			return this.Name;
		}
	}

	public class InstanceFieldAccess : IFieldAccess, IAssignableValue, IReferenceable
	{
		public IFieldReference Field { get; set; }
		public IVariable Instance { get; set; }

		public InstanceFieldAccess(IVariable instance, IFieldReference field)
		{
			this.Instance = instance;
			this.Field = field;
		}

		public string FieldName
		{
			get { return this.Field.Name; }
		}

		public string Name
		{
			get { return string.Format("{0}.{1}", this.Instance, this.FieldName); }
		}

		public IType Type
		{
			get { return this.Field.Type; }
		}

		public ISet<IVariable> Variables
		{
			get { return new HashSet<IVariable>() { this.Instance }; }
		}

		public void Replace(IVariable oldvar, IVariable newvar)
		{
			if (this.Instance.Equals(oldvar)) this.Instance = newvar;
		}

		IExpression IExpression.Replace(IExpression oldexpr, IExpression newexpr)
		{
			if (this.Equals(oldexpr)) return newexpr;
			var result = this;

			if (oldexpr is IVariable && newexpr is IVariable)
			{
				var instance = (this.Instance as IExpression).Replace(oldexpr, newexpr) as IVariable;
				result = new InstanceFieldAccess(instance, this.Field);
			}

			return result;
		}

		IExpression IExpressible.ToExpression()
		{
			return this;
		}

		public override bool Equals(object obj)
		{
			if (object.ReferenceEquals(this, obj)) return true;
			var other = obj as InstanceFieldAccess;

			return other != null &&
				this.Name.Equals(other.Name);
		}

		public override int GetHashCode()
		{
			return this.Name.GetHashCode();
		}

		public override string ToString()
		{
			return this.Name;
		}
	}

	public class ArrayLengthAccess : IFieldAccess
	{
		public IVariable Instance { get; set; }

		public ArrayLengthAccess(IVariable instance)
		{
			this.Instance = instance;
		}

		public string FieldName
		{
			get { return "Length"; }
		}

		public string Name
		{
			get { return string.Format("{0}.{1}", this.Instance, this.FieldName); }
		}

		public IType Type
		{
			get { return PlatformTypes.ArrayLengthType; }
		}

		public ISet<IVariable> Variables
		{
			get { return new HashSet<IVariable>() { this.Instance }; }
		}

		public void Replace(IVariable oldvar, IVariable newvar)
		{
			if (this.Instance.Equals(oldvar)) this.Instance = newvar;
		}

		IExpression IExpression.Replace(IExpression oldexpr, IExpression newexpr)
		{
			if (this.Equals(oldexpr)) return newexpr;
			var result = this;

			if (oldexpr is IVariable && newexpr is IVariable)
			{
				var instance = (this.Instance as IExpression).Replace(oldexpr, newexpr) as IVariable;
				result = new ArrayLengthAccess(instance);
			}

			return result;
		}

		IExpression IExpressible.ToExpression()
		{
			return this;
		}

		public override bool Equals(object obj)
		{
			if (object.ReferenceEquals(this, obj)) return true;
			var other = obj as ArrayLengthAccess;

			return other != null &&
				this.Name.Equals(other.Name);
		}

		public override int GetHashCode()
		{
			return this.Name.GetHashCode();
		}

		public override string ToString()
		{
			return this.Name;
		}
	}

	public class ArrayElementAccess : IAssignableValue, IReferenceable, IExpression
	{
		public IVariable Array { get; set; }
		public IList<IVariable> Indices { get; set; }

		public ArrayElementAccess(IVariable array, IEnumerable<IVariable> indices)
		{
			this.Array = array;
			this.Indices = new List<IVariable>(indices);
		}

        public ArrayElementAccess(IVariable array, IVariable index)
        {
            this.Array = array;
            this.Indices = new List<IVariable>() { index };
        }


        public IType Type
		{
			get
			{
				var arrayType = this.Array.Type as ArrayType;
				return arrayType.ElementsType;
			}
		}

		public ISet<IVariable> Variables
		{
			get { return new HashSet<IVariable>(this.Indices) { this.Array  }; }
		}

		public void Replace(IVariable oldvar, IVariable newvar)
		{
			if (this.Array.Equals(oldvar)) this.Array = newvar;
            for (int i = 0; i < Indices.Count; i++)
            {
                if (this.Indices[i].Equals(oldvar)) this.Indices[i] = newvar;
            }
		}

		IExpression IExpression.Replace(IExpression oldexpr, IExpression newexpr)
		{
			if (this.Equals(oldexpr)) return newexpr;
			var result = this;

			if (oldexpr is IVariable && newexpr is IVariable)
			{
				var array = (this.Array as IExpression).Replace(oldexpr, newexpr) as IVariable;
                var indices = new List<IVariable>();

                for (int i = 0; i < Indices.Count; i++)
                {

                    var index = (this.Indices[i] as IExpression).Replace(oldexpr, newexpr) as IVariable;
                    indices.Add(index);
                }

				result = new ArrayElementAccess(array, indices);
			}

			return result;
		}

		IExpression IExpressible.ToExpression()
		{
			return this;
		}

		public override bool Equals(object obj)
		{
			if (object.ReferenceEquals(this, obj)) return true;
			var other = obj as ArrayElementAccess;

			return other != null &&
				this.Array.Equals(other.Array) &&
				this.Indices.SequenceEqual(other.Indices);
		}

		public override int GetHashCode()
		{
			return this.Array.GetHashCode() ^
				this.Indices.GetHashCode();
		}

		public override string ToString()
		{
            var indices = string.Join(", ",this.Indices);
			return string.Format("{0}[{1}]", this.Array, indices);
		}
	}

	public class Dereference : IAssignableValue, IReferenceable, IExpression
	{
		public IVariable Reference { get; set; }

		public Dereference(IVariable reference)
		{
			this.Reference = reference;
		}

		public IType Type
		{
			get
			{
                var pointerType = new PointerType(this.Reference.Type);
				//var pointerType = this.Reference.Type as PointerType;
				return pointerType.TargetType;
			}
		}

		public ISet<IVariable> Variables
		{
			get { return new HashSet<IVariable>() { this.Reference }; }
		}

		public void Replace(IVariable oldvar, IVariable newvar)
		{
			if (this.Reference.Equals(oldvar)) this.Reference = newvar;
		}

		IExpression IExpression.Replace(IExpression oldexpr, IExpression newexpr)
		{
			if (this.Equals(oldexpr)) return newexpr;
			var result = this;

			if (oldexpr is IVariable && newexpr is IVariable)
			{
				var reference = (this.Reference as IExpression).Replace(oldexpr, newexpr) as IVariable;
				result = new Dereference(reference);
			}

			return result;
		}

		IExpression IExpressible.ToExpression()
		{
			return this;
		}

		public override bool Equals(object obj)
		{
			if (object.ReferenceEquals(this, obj)) return true;
			var other = obj as Dereference;

			return other != null &&
				this.Reference.Equals(other.Reference);
		}

		public override int GetHashCode()
		{
			return this.Reference.GetHashCode();
		}

		public override string ToString()
		{
			return string.Format("*{0}", this.Reference);
		}
	}

	public class Reference : IValue, IExpression
	{
		public IReferenceable Value { get; set; }

		public Reference(IReferenceable value)
		{
			this.Value = value;
		}

		public IType Type
		{
			get { return new PointerType(this.Value.Type); }
		}

		public ISet<IVariable> Variables
		{
			get { return new HashSet<IVariable>(this.Value.Variables); }
		}

		public void Replace(IVariable oldvar, IVariable newvar)
		{
			if (this.Value.Equals(oldvar)) this.Value = newvar;
			else (this.Value as IVariableContainer).Replace(oldvar, newvar);
		}

		IExpression IExpression.Replace(IExpression oldexpr, IExpression newexpr)
		{
			if (this.Equals(oldexpr)) return newexpr;

			var value = (this.Value as IExpression).Replace(oldexpr, newexpr) as IReferenceable;
			var result = new Reference(value);

			return result;
		}

		IExpression IExpressible.ToExpression()
		{
			return this;
		}

		public override bool Equals(object obj)
		{
			if (object.ReferenceEquals(this, obj)) return true;
			var other = obj as Reference;

			return other != null &&
				this.Value.Equals(other.Value);
		}

		public override int GetHashCode()
		{
			return this.Value.GetHashCode();
		}

		public override string ToString()
		{
			return string.Format("&{0}", this.Value);
		}
	}
}
