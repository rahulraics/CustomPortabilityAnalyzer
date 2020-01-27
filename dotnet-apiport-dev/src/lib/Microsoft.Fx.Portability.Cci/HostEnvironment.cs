﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Fx.Portability.Cci.Resources;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

#if FEATURE_DIAGNOSTICS_CONTRACT
using System.Diagnostics.Contracts;
#endif

namespace Microsoft.Cci.Extensions
{
    public enum ErrorTreatment
    {
        Default,
        TreatAsWarning,
        Ignore,
    }

    public class HostEnvironment : MetadataReaderHost
    {
        private PeReader _reader;
        private AssemblyIdentity _coreAssemblyIdentity;

        public HostEnvironment()
            : this(new NameTable(), new InternFactory())
        {
        }

        public HostEnvironment(INameTable nameTable)
            : this(nameTable, new InternFactory())
        {
        }

        public HostEnvironment(IInternFactory internFactory)
            : this(new NameTable(), internFactory)
        {
        }

        public HostEnvironment(INameTable nameTable, IInternFactory internFactory)
            : base(nameTable, internFactory, 0, null, false)
        {
            _reader = new PeReader(this);
            UnresolvedIdentities = new HashSet<UnresolvedReference<IUnit, AssemblyIdentity>>();
        }

        public bool UnifyToLibPath { get; set; }

        public ICollection<UnresolvedReference<IUnit, AssemblyIdentity>> UnresolvedIdentities { get; }

        public bool ResolveInReferringUnitLocation { get; set; }

        public bool ResolveAgainstRunningFramework { get; set; }

        public event EventHandler<UnresolvedReference<IUnit, AssemblyIdentity>> UnableToResolve;

        public void AddLibPaths(IEnumerable<string> paths)
        {
            if (paths == null)
            {
                return;
            }

            foreach (var path in paths)
            {
                AddLibPath(path);
            }
        }

        public void Cleanup()
        {
            _reader = null;
        }

        public override IUnit LoadUnitFrom(string location)
        {
            IUnit unit = _reader.OpenModule(
                BinaryDocument.GetBinaryDocumentForFile(location, this));

            RegisterAsLatest(unit);
            return unit;
        }

        public IAssembly LoadAssemblyFrom(string location)
        {
            return LoadUnitFrom(location) as IAssembly;
        }

        /// <summary>
        /// Loads the unit from the given stream. The caller should dispose the
        /// stream after the API is called (the stream contents will have been copied
        /// to unmanaged memory already).
        /// </summary>
        /// <param name="location">The location to be exposed from IUnit.</param>
        /// <param name="stream">The data to be used as the unit.</param>
        public IUnit LoadUnitFrom(string location, Stream stream)
        {
            var fileName = Path.GetFileName(location);
            var name = NameTable.GetNameFor(fileName);
            var document = new StreamDocument(location, name, stream);
            var unit = _reader.OpenModule(document);

            this.RegisterAsLatest(unit);
            return unit;
        }

        public IAssembly LoadAssemblyFrom(string location, Stream stream)
        {
            return LoadUnitFrom(location, stream) as IAssembly;
        }

        public IAssembly LoadAssembly(string assemblyNameOrPath)
        {
            string path = assemblyNameOrPath;

            if (File.Exists(path))
            {
                return LoadAssemblyFrom(path);
            }

            foreach (var extension in ProbingExtensions)
            {
                path = ProbeLibPaths(assemblyNameOrPath + extension);
                if (path != null)
                {
                    var assembly = this.LoadAssembly(path);
                    if (assembly == null)
                    {
                        continue;
                    }

                    return assembly;
                }
            }

            return null;
        }

        private AssemblyIdentity ProbeLibPaths(AssemblyIdentity identity)
        {
            foreach (var libPath in LibPaths)
            {
                AssemblyIdentity probedIdentity = this.Probe(libPath, identity);
                if (probedIdentity != null)
                {
                    return probedIdentity;
                }
            }

            return new AssemblyIdentity(identity, string.Empty);
        }

        private string ProbeLibPaths(string assemblyPath)
        {
            if (File.Exists(assemblyPath))
            {
                return assemblyPath;
            }

            foreach (var libPath in LibPaths)
            {
                string combinedPath = Path.Combine(libPath, assemblyPath);
                if (File.Exists(combinedPath))
                {
                    return combinedPath;
                }
            }

            return null;
        }

        // Potential way to unify assemblies based on the current runtime
        // public override void ResolvingAssemblyReference(IUnit referringUnit, AssemblyIdentity referencedAssembly)
        // {
        //    IAssemblyReference asmRef = referringUnit.UnitReferences.OfType<IAssemblyReference>()
        //        .FirstOrDefault(a => referencedAssembly.Equals(a.UnifiedAssemblyIdentity));
        //
        //    if (asmRef != null && asmRef.IsRetargetable)
        //    {
        //        string strongName = UnitHelper.StrongName(asmRef);
        //        string retargetedName = AppDomain.CurrentDomain.ApplyPolicy(strongName);
        //        if (strongName != retargetedName)
        //        {
        //            System.Reflection.AssemblyName name = new System.Reflection.AssemblyName(retargetedName);
        //
        //            referencedAssembly = new AssemblyIdentity(this.NameTable.GetNameFor(name.Name),
        //                name.CultureInfo != null ? name.CultureInfo.Name : "", name.Version, name.GetPublicKeyToken(), "");
        //        }
        //    }
        //    base.ResolvingAssemblyReference(referringUnit, referencedAssembly);
        // }
        private static readonly string[] ProbingExtensions = new string[]
        {
            ".dll",
            ".ildll",
            ".ni.dll",
            ".winmd",
            ".exe",
            ".ilexe",

            // ".ni.exe" Do these actually exist?
        };

        protected override AssemblyIdentity Probe(string probeDir, AssemblyIdentity referencedAssembly)
        {
#if FEATURE_DIAGNOSTICS_CONTRACT
            Contract.Requires(probeDir != null);
            Contract.Requires(referencedAssembly != null);
#endif
            string path = null;
            foreach (var extension in ProbingExtensions)
            {
                path = Path.Combine(probeDir, referencedAssembly.Name.Value + extension);
                if (File.Exists(path))
                {
                    // Possible that we might find an assembly with a matching extension but without a match identity
                    // or possibly be a native version of the assembly so if that fails we should try other extensions.
                    if (!(LoadUnitFrom(path) is IAssembly assembly))
                    {
                        continue;
                    }

                    if (UnifyToLibPath)
                    {
                        // If Unifying to LibPath then we only verify the assembly name matches.
                        if (assembly.AssemblyIdentity.Name.UniqueKeyIgnoringCase != referencedAssembly.Name.UniqueKeyIgnoringCase)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        if (!assembly.AssemblyIdentity.Equals(referencedAssembly))
                        {
                            continue;
                        }
                    }

                    return assembly.AssemblyIdentity;
                }
            }

            return null;
        }

        protected override AssemblyIdentity GetCoreAssemblySymbolicIdentity()
        {
            // If explicitly set return that identity
            if (_coreAssemblyIdentity != null)
            {
                return _coreAssemblyIdentity;
            }

            AssemblyIdentity baseCoreAssemblyIdentity = base.GetCoreAssemblySymbolicIdentity();

            // Try to find the assembly which believes itself is the core assembly
            foreach (var assembly in this.LoadedUnits.OfType<IAssembly>())
            {
                if (assembly.AssemblyIdentity.Equals(assembly.CoreAssemblySymbolicIdentity))
                {
                    return assembly.AssemblyIdentity;
                }

                // Adjust the base core assembly identity based on what this assembly believes should be
                if (assembly.AssemblyIdentity.Equals(baseCoreAssemblyIdentity))
                {
                    baseCoreAssemblyIdentity = assembly.CoreAssemblySymbolicIdentity;
                }
            }

            // Otherwise fallback to CCI's default core assembly loading logic.
            return baseCoreAssemblyIdentity;
        }

        public void SetCoreAssembly(AssemblyIdentity coreAssembly)
        {
            if (_coreAssemblyIdentity != null)
            {
                throw new InvalidOperationException(LocalizedStrings.CoreAssemblyAlreadySet);
            }

            // Lets ignore this if someone passes dummy as nothing good can come from it. We considered making it an error
            // but in some logical cases (i.e. facades) the CoreAssembly might be dummy and we don't want to start throwing
            // in a bunch of cases where if we let it go the right thing will happen.
            if (coreAssembly == Dummy.AssemblyIdentity)
            {
                return;
            }

            _coreAssemblyIdentity = coreAssembly;
        }

        private AssemblyIdentity FindUnifiedAssemblyIdentity(AssemblyIdentity identity)
        {
#if FEATURE_DIAGNOSTICS_CONTRACT
            Contract.Assert(this.UnifyToLibPath);
#endif
            // Find exact assembly match
            IAssembly asm = this.FindAssembly(identity);

            if (asm != null && !(asm is Dummy))
            {
                return asm.AssemblyIdentity;
            }

            // Find assembly match based on simple name only. (It might be worth caching these results if we find them to be too expensive)
            foreach (var loadedAssembly in this.LoadedUnits.OfType<IAssembly>())
            {
                if (loadedAssembly.AssemblyIdentity.Name.UniqueKeyIgnoringCase == identity.Name.UniqueKeyIgnoringCase)
                {
                    return loadedAssembly.AssemblyIdentity;
                }
            }

            var probedIdentity = ProbeLibPaths(identity);
            if (probedIdentity != null)
            {
                return probedIdentity;
            }

            return new AssemblyIdentity(identity, string.Empty);
        }

        /// <summary>
        /// Default implementation of UnifyAssembly. Override this method to change the behavior.
        /// </summary>
        public override AssemblyIdentity UnifyAssembly(AssemblyIdentity assemblyIdentity)
        {
            if (ShouldUnifyToCoreAssembly(assemblyIdentity))
            {
                return this.CoreAssemblySymbolicIdentity;
            }

            if (this.UnifyToLibPath)
            {
                assemblyIdentity = FindUnifiedAssemblyIdentity(assemblyIdentity);
            }

            return assemblyIdentity;
        }

        // Managed WinMDs: Their 'BCL' reference looks like this:
        // .assembly extern mscorlib
        // {
        //   .publickeytoken = (B7 7A 5C 56 19 34 E0 89 )
        //   .ver 255:255:255:255
        // }
        private static readonly byte[] EcmaKey = { 0xb7, 0x7a, 0x5c, 0x56, 0x19, 0x34, 0xe0, 0x89 };

        private static readonly Version WinmdBclVersion = new Version(255, 255, 255, 255);

        public bool ShouldUnifyToCoreAssembly(AssemblyIdentity assemblyIdentity)
        {
            // Unify any other potential versions of this core assembly to itself.
            if (assemblyIdentity.Name.UniqueKeyIgnoringCase == this.CoreAssemblySymbolicIdentity.Name.UniqueKeyIgnoringCase)
            {
                if (assemblyIdentity.PublicKeyToken == null ||
                   !assemblyIdentity.PublicKeyToken.SequenceEqual(this.CoreAssemblySymbolicIdentity.PublicKeyToken))
                {
                    return false;
                }

                return true;
            }

            // Unify the mscorlib 255.255.255.255 used by winmds back to corefx to avoid the need for yet
            // another facade.
            if (assemblyIdentity.Name.Value == "mscorlib")
            {
                if (assemblyIdentity.PublicKeyToken == null || !assemblyIdentity.PublicKeyToken.SequenceEqual(EcmaKey))
                {
                    return false;
                }

                if (!assemblyIdentity.Version.Equals(WinmdBclVersion))
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        ///  Override ProbeAssemblyReference to ensure we only look in the LibPaths for resolving assemblies and
        ///  we don't accidently find some in the GAC or in the framework directory.
        /// </summary>
        public override AssemblyIdentity ProbeAssemblyReference(IUnit referringUnit, AssemblyIdentity referencedAssembly)
        {
            // We need to ensure the core assembly is being unified and in some code paths, such as from GetCoreAssemblySymbolicIdentity
            // it doesn't get properly unified before calling ProbeAssemblyReference
            if (CoreAssemblySymbolicIdentity.Equals(referencedAssembly))
            {
                referencedAssembly = UnifyAssembly(referencedAssembly);
            }

            AssemblyIdentity result = null;

            if (this.ResolveInReferringUnitLocation)
            {
                // NOTE: When probing for the core assembly, the referring unit is a dummy unit and thus does not have
                //       a location.
                var referringDir = string.IsNullOrEmpty(referringUnit.Location) ? null
                                     : Path.GetDirectoryName(Path.GetFullPath(referringUnit.Location));

                result = string.IsNullOrEmpty(referringDir) ? null
                       : Probe(referringDir, referencedAssembly);

                if (result != null)
                {
                    return result;
                }
            }

            // Probe in the libPaths directories
            foreach (string libPath in this.LibPaths)
            {
                result = this.Probe(libPath, referencedAssembly);
                if (result != null)
                {
                    return result;
                }
            }

            if (this.ResolveAgainstRunningFramework)
            {
                // Call base probe which has logic to check the frameworks installed on the machine
                result = base.ProbeAssemblyReference(referringUnit, referencedAssembly);

                if (result != null && result.Location != null && !result.Location.StartsWith("unknown", StringComparison.Ordinal))
                {
                    return result;
                }
            }

            var unresolved = new UnresolvedReference<IUnit, AssemblyIdentity>(referringUnit, referencedAssembly);

            OnUnableToResolve(unresolved);

            // Give up
            return new AssemblyIdentity(referencedAssembly, "unknown://location");
        }

        protected virtual void OnUnableToResolve(UnresolvedReference<IUnit, AssemblyIdentity> unresolved)
        {
            UnableToResolve?.Invoke(this, unresolved);

            UnresolvedIdentities.Add(unresolved);
        }

        // Overriding this method allows us to read the binaries without blocking the files. The default
        // implementation will use a memory mapped file (MMF) which causes the files to be locked. That
        // means you can delete them, but you can't overwrite them in-palce, which is especially painful
        // when reading binaries directly from a build ouput folder.
        //
        // Measuring indicated that performance implications are negligible. That's why we decided to
        // make this the default and not exposing any (more) options to our ctor.
        public override IBinaryDocumentMemoryBlock OpenBinaryDocument(IBinaryDocument sourceDocument)
        {
            // First let's see whether the document is a stream-based document. In that case, we'll
            // call the overload that processes the stream.
            var streamDocument = sourceDocument as StreamDocument;
            if (streamDocument != null)
            {
                return UnmanagedBinaryMemoryBlock.CreateUnmanagedBinaryMemoryBlock(streamDocument.Stream, sourceDocument);
            }

            // Otherwise we assume that we can load the data from the location of sourceDocument.
            try
            {
                var memoryBlock = UnmanagedBinaryMemoryBlock.CreateUnmanagedBinaryMemoryBlock(sourceDocument.Location, sourceDocument);
                disposableObjectAllocatedByThisHost.Add(memoryBlock);
                return memoryBlock;
            }
            catch (IOException)
            {
                return null;
            }
        }

#region Assembly Set and Path Helpers

        public static string[] SplitPaths(string pathSet)
        {
            if (pathSet == null)
            {
                return new string[0];
            }

            return pathSet.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static IEnumerable<IAssembly> LoadAssemblySet(params string[] paths)
        {
            HostEnvironment host = new HostEnvironment();
            return host.LoadAssemblies(paths);
        }

        private static string GetCoreAssemblyFile(string coreAssemblySimpleName, IEnumerable<string> contractSet)
        {
            var coreAssemblyFile = contractSet.FirstOrDefault(c => Path.GetFileNameWithoutExtension(c).EndsWith(coreAssemblySimpleName, StringComparison.OrdinalIgnoreCase) == true);
            if (string.IsNullOrEmpty(coreAssemblyFile))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, LocalizedStrings.CoreAssemblyNotFoundInContracts, coreAssemblySimpleName));
            }

            return coreAssemblyFile;
        }

        public ReadOnlyCollection<IAssembly> LoadAssemblies(string unsplitContractSet)
        {
            return LoadAssemblies(unsplitContractSet, string.Empty, null);
        }

        public ReadOnlyCollection<IAssembly> LoadAssemblies(string unsplitContractSet, string coreAssemblySimpleName, Action<string, ErrorTreatment> logErrorCallback)
        {
            List<string> contractSet = new List<string>(GetFilePathsAndAddResolvedDirectoriesToLibPaths(SplitPaths(unsplitContractSet)));
            string coreAssemblyFile = null;

            if (!string.IsNullOrEmpty(coreAssemblySimpleName))
            {
                // Otherwise, rearange the list such that the specified coreAssembly is the first one in the list.
                coreAssemblyFile = GetCoreAssemblyFile(coreAssemblySimpleName, contractSet);

                contractSet.Remove(coreAssemblyFile);
                contractSet.Insert(0, coreAssemblyFile);
            }

            ReadOnlyCollection<IAssembly> assemblies = LoadAssemblies(contractSet, logErrorCallback);

            // Explicitly set the core assembly
            if (coreAssemblyFile != null && assemblies.Count > 0)
            {
                SetCoreAssembly(assemblies[0].AssemblyIdentity);
            }

            return assemblies;
        }

        public ErrorTreatment LoadErrorTreatment
        {
            get;
            set;
        }

        // False by deafult for backwards compatibility with tools that wire in their own custom handlers.
        private bool _traceResolutionErrorsAsLoadErrors;

        public bool TraceResolutionErrorsAsLoadErrors
        {
            get
            {
                return _traceResolutionErrorsAsLoadErrors;
            }

            set
            {
                if (value != _traceResolutionErrorsAsLoadErrors)
                {
                    if (value)
                    {
                        this.UnableToResolve += TraceResolveErrorAsLoadError;
                    }
                    else
                    {
                        this.UnableToResolve -= TraceResolveErrorAsLoadError;
                    }

                    _traceResolutionErrorsAsLoadErrors = value;
                }
            }
        }

        private void TraceResolveErrorAsLoadError(object sender, UnresolvedReference<IUnit, AssemblyIdentity> e)
        {
            TraceLoadError("Unable to resolve reference to {0}.", e.Unresolved);
        }

        public void TraceLoadError(string format, params object[] arguments)
        {
            TraceErrorWithLevel(LoadErrorTreatment, format, arguments);
        }

        public static void TraceErrorWithLevel(ErrorTreatment level, string format, params object[] arguments)
        {
            switch (level)
            {
                case ErrorTreatment.Default:
                default:
                    Trace.TraceError(format, arguments);
                    break;

                case ErrorTreatment.TreatAsWarning:
                    Trace.TraceWarning(format, arguments);
                    break;

                case ErrorTreatment.Ignore:
                    break;
            }
        }

        public ReadOnlyCollection<IAssembly> LoadAssemblies(IEnumerable<string> paths)
        {
            return LoadAssemblies(paths, (message, errorLevel) =>
                {
                    TraceLoadError(message);
                });
        }

        public ReadOnlyCollection<IAssembly> LoadAssemblies(IEnumerable<string> paths, Action<string, ErrorTreatment> logErrorCallback)
        {
            List<IAssembly> assemblySet = new List<IAssembly>();
            IAssembly assembly = null;

            foreach (string file in GetFilePathsAndAddResolvedDirectoriesToLibPaths(paths))
            {
                string filePath = ProbeLibPaths(file);
                if (filePath == null)
                {
                    logErrorCallback?.Invoke(string.Format(CultureInfo.CurrentCulture, "File does not exist {0}", file), LoadErrorTreatment);
                    continue;
                }

                assembly = LoadAssembly(filePath);
                if (assembly == null)
                {
                    logErrorCallback?.Invoke(string.Format(CultureInfo.CurrentCulture, "Failed to load assembly {0}", filePath), LoadErrorTreatment);
                    continue;
                }

                assemblySet.Add(assembly);
            }

            if (assemblySet.Count == 0)
            {
                logErrorCallback?.Invoke(string.Format(CultureInfo.CurrentCulture, "No assemblies loaded for {0}", string.Join(", ", paths)), LoadErrorTreatment);
            }

            return new ReadOnlyCollection<IAssembly>(assemblySet);
        }

        public ReadOnlyCollection<IAssembly> LoadAssemblies(IEnumerable<string> paths, string coreAssemblySimpleName)
        {
            // Re-arrange the list of paths so that the coreAssembly is the first one in the list.
            if (!string.IsNullOrEmpty(coreAssemblySimpleName))
            {
                var coreAssemblyFile = GetCoreAssemblyFile(coreAssemblySimpleName, paths);

                paths = Enumerable.Concat(new List<string>() { coreAssemblyFile }, paths.Where(ai => !StringComparer.OrdinalIgnoreCase.Equals(ai, coreAssemblyFile)));
            }

            return LoadAssemblies(paths);
        }

        public IEnumerable<IAssembly> LoadAssemblies(IEnumerable<AssemblyIdentity> identities)
        {
            return LoadAssemblies(identities, false);
        }

        public IEnumerable<IAssembly> LoadAssemblies(IEnumerable<AssemblyIdentity> identities, bool warnOnVersionMismatch)
        {
            return LoadAssemblies(identities, warnOnVersionMismatch, (message, errorTreatment) =>
                {
                    TraceErrorWithLevel(errorTreatment, message);
                });
        }

        /// <summary>
        /// Method that loads assemblies given a list of identities.
        /// </summary>
        /// <param name="identities">Collection of identities to load.</param>
        /// <param name="logErrorOrWarningCallback">Callback method that takes a message(string), an error level(ErrorTreatment), and a flag for isVersionMismatch(bool).</param>
        /// <returns>Collection of Assemblies.</returns>
        public IEnumerable<IAssembly> LoadAssemblies(IEnumerable<AssemblyIdentity> identities, bool warnOnVersionMismatch, Action<string, ErrorTreatment> logErrorOrWarningCallback)
        {
            List<IAssembly> matchingAssemblies = new List<IAssembly>();
            foreach (var unmappedIdentity in identities)
            {
                // Remap the name and clear the location.
                var identity = new AssemblyIdentity(this.NameTable.GetNameFor(unmappedIdentity.Name.Value),
                    unmappedIdentity.Culture, unmappedIdentity.Version, unmappedIdentity.PublicKeyToken, string.Empty);

                var matchingIdentity = this.ProbeLibPaths(identity);

                var matchingAssembly = this.LoadAssembly(matchingIdentity);
                if ((matchingAssembly == null || matchingAssembly == Dummy.Assembly) && logErrorOrWarningCallback != null)
                {
                    var message = string.Format(CultureInfo.CurrentCulture, "Failed to find or load matching assembly '{0}'.", identity.Name.Value);
                    logErrorOrWarningCallback(message, LoadErrorTreatment);
                    continue;
                }

                if (!identity.Version.Equals(matchingAssembly.Version) && logErrorOrWarningCallback != null && warnOnVersionMismatch)
                {
                    var message = string.Format(CultureInfo.CurrentCulture, "Found '{0}' with version '{1}' instead of '{2}'.", identity.Name.Value, matchingAssembly.Version, identity.Version);
                    logErrorOrWarningCallback(message, ErrorTreatment.TreatAsWarning);
                }

                string idPKT = identity.GetPublicKeyToken();
                string matchingPKT = matchingAssembly.GetPublicKeyToken();

                if (!idPKT.Equals(matchingPKT, StringComparison.OrdinalIgnoreCase) && logErrorOrWarningCallback != null)
                {
                    string message = string.Format(CultureInfo.CurrentCulture, "Found '{0}' with PublicKeyToken '{1}' instead of '{2}'.", identity.Name.Value, matchingPKT, idPKT);
                    logErrorOrWarningCallback(message, ErrorTreatment.TreatAsWarning);
                }

                matchingAssemblies.Add(matchingAssembly);
            }

            return matchingAssemblies;
        }

        public IEnumerable<IAssembly> LoadAssemblies(IEnumerable<AssemblyIdentity> identities, bool warnOnVersionMismatch, string coreAssemblySimpleName)
        {
            // Re-arrange the list of identities so that the coreIdentity is the first one in the list.
            if (!string.IsNullOrEmpty(coreAssemblySimpleName))
            {
                var coreIdentity = identities.FirstOrDefault(ai => StringComparer.OrdinalIgnoreCase.Equals(ai.Name.Value, coreAssemblySimpleName));

                if (coreIdentity == null)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, LocalizedStrings.CoreAssemblyNotFoundInIdentities, coreAssemblySimpleName));
                }

                identities = Enumerable.Concat(new List<AssemblyIdentity>() { coreIdentity }, identities.Where(ai => ai != coreIdentity));
            }

            return LoadAssemblies(identities, warnOnVersionMismatch);
        }

        public static IEnumerable<string> GetFilePaths(IEnumerable<string> paths, SearchOption searchOption)
        {
            if (searchOption == SearchOption.TopDirectoryOnly)
            {
                return GetFilePaths(paths);
            }

            // expand the path into a list of paths that contains all the subdirectories
            var unexpandedPaths = new Stack<string>(paths);
            var allPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in paths)
            {
                allPaths.Add(path);

                // if the path did not point to a directory, continue
                if (!Directory.Exists(path))
                {
                    continue;
                }

                foreach (var dir in Directory.EnumerateDirectories(path, "*.*", SearchOption.AllDirectories))
                {
                    allPaths.Add(dir);
                }
            }

            // make sure we remove any duplicated folders (ie. if the user specified both a root folder and a leaf one)
            return GetFilePaths(allPaths);
        }

        public static IEnumerable<string> GetFilePaths(IEnumerable<string> paths)
        {
            return GetFilePaths(paths, (resolvedPath) => { });
        }

        private IEnumerable<string> GetFilePathsAndAddResolvedDirectoriesToLibPaths(IEnumerable<string> paths)
        {
            return GetFilePaths(paths, (resolvedPath) => this.LibPaths.Add(resolvedPath));
        }

        private static IEnumerable<string> GetFilePaths(IEnumerable<string> paths, Action<string> perResolvedPathAction, bool recursive = false)
        {
            foreach (var path in paths)
            {
                if (path == null)
                {
                    continue;
                }

                string resolvedPath = Environment.ExpandEnvironmentVariables(path);

                if (Directory.Exists(resolvedPath))
                {
                    perResolvedPathAction(resolvedPath);

                    for (int extIndex = 0; extIndex < ProbingExtensions.Length; extIndex++)
                    {
                        var searchPattern = "*" + ProbingExtensions[extIndex];
                        foreach (var file in Directory.EnumerateFiles(resolvedPath, searchPattern))
                        {
                            yield return file;
                        }
                    }

                    if (recursive)
                    {
                        // Recursively do the same for sub-folders
                        foreach (var file in GetFilePaths(Directory.EnumerateDirectories(resolvedPath), perResolvedPathAction, recursive))
                        {
                            yield return file;
                        }
                    }
                }
                else if (Path.GetFileName(resolvedPath).Contains('*'))
                {
                    IEnumerable<string> files;

                    // Cannot yield a value in the body of a try-catch with catch clause.
                    try
                    {
                        files = Directory.EnumerateFiles(Path.GetDirectoryName(resolvedPath), Path.GetFileName(resolvedPath));
                    }
                    catch (ArgumentException)
                    {
                        files = new[] { resolvedPath };
                    }

                    foreach (var file in files)
                    {
                        yield return file;
                    }
                }
                else
                {
                    yield return resolvedPath;
                }
            }
        }

#endregion

        private sealed class StreamDocument : IBinaryDocument
        {
            private readonly string _location;
            private readonly IName _name;
            private readonly Stream _stream;

            public StreamDocument(string location, IName name, Stream stream)
            {
                _stream = stream;
                _location = location;
                _name = name;
            }

            public string Location
            {
                get { return _location; }
            }

            public IName Name
            {
                get { return _name; }
            }

            public Stream Stream
            {
                get { return _stream; }
            }

            public uint Length
            {
                get { return (uint)_stream.Length; }
            }
        }
    }

    public class UnresolvedReference<TReferrer, TUnresolved> : EventArgs
    {
        public UnresolvedReference(TReferrer referrer, TUnresolved unresolvedReference)
        {
            this.Referrer = referrer;
            this.Unresolved = unresolvedReference;
        }

        public TReferrer Referrer { get; private set; }

        public TUnresolved Unresolved { get; private set; }
    }
}
