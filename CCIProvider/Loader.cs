﻿// Copyright (c) Edgardo Zoppi.  All Rights Reserved.  Licensed under the MIT License.  See License.txt in the project root for license information.

using Model;
using Model.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cci = Microsoft.Cci;

namespace CCIProvider
{
	public class Loader : IDisposable
	{
		protected Host ourHost;
        protected Cci.MetadataReaderHost cciHost;

        protected IDictionary<Assembly, Cci.AssemblyIdentity> cciAssemblyMap;

        

        public Loader(Host host)
		{
			this.ourHost = host;
			this.cciHost = new Cci.PeReader.DefaultHost();
            this.cciAssemblyMap = new Dictionary<Assembly, Cci.AssemblyIdentity>();
            
        }

		public void Dispose()
		{
			this.cciHost.Dispose();
			this.cciHost = null;
			this.ourHost = null;
			GC.SuppressFinalize(this);
		}

		public Assembly LoadCoreAssembly()
		{
			var module = cciHost.LoadUnit(cciHost.CoreAssemblySymbolicIdentity) as Cci.IModule;

			if (module == null || module == Cci.Dummy.Module || module == Cci.Dummy.Assembly)
				throw new Exception("The input is not a valid CLR module or assembly.");

			var assembly = this.ExtractAssembly(module, null);

			ourHost.Assemblies.Add(assembly);

            cciAssemblyMap[assembly] = module.ContainingAssembly.AssemblyIdentity;

			return assembly;
		}

		public Assembly LoadAssembly(string fileName)
		{
			var module = cciHost.LoadUnitFrom(fileName) as Cci.IModule;

			if (module == null || module == Cci.Dummy.Module || module == Cci.Dummy.Assembly)
				throw new Exception("The input is not a valid CLR module or assembly.");

			var pdbFileName = Path.ChangeExtension(fileName, "pdb");
			Cci.PdbReader pdbReader = null;

			if (File.Exists(pdbFileName))
			{
				using (var pdbStream = File.OpenRead(pdbFileName))
				{
					pdbReader = new Cci.PdbReader(pdbStream, cciHost);
				}
			}
			var assembly = this.ExtractAssembly(module, pdbReader);

			if (pdbReader != null)
			{
				pdbReader.Dispose();
			}

			ourHost.Assemblies.Add(assembly);
            cciAssemblyMap[assembly] = module.ContainingAssembly.AssemblyIdentity;
            
            return assembly;
		}
      

        private Assembly ExtractAssembly(Cci.IModule module, Cci.PdbReader pdbReader)
		{
			var traverser = new AssemblyTraverser(ourHost, cciHost, pdbReader);
			traverser.Traverse(module.ContainingAssembly);
			var result = traverser.Result;
			return result;
		}
	}
}
