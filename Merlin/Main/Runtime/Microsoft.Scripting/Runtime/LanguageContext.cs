/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Microsoft Public License. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Microsoft Public License, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Microsoft Public License.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Dynamic;
using System.Text;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Utils;
using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace Microsoft.Scripting.Runtime {
    /// <summary>
    /// Provides language specific facilities which are typically called by the runtime.
    /// </summary>
    public abstract class LanguageContext {
        private readonly ScriptDomainManager _domainManager;
        private ActionBinder _binder;
        private readonly ContextId _id;

        protected LanguageContext(ScriptDomainManager domainManager) {
            ContractUtils.RequiresNotNull(domainManager, "domainManager");

            _domainManager = domainManager;
            _id = domainManager.GenerateContextId();
        }

        public ActionBinder Binder {
            get {
                return _binder;
            }
            protected set {
                _binder = value;
            }
        }

        /// <summary>
        /// Provides the ContextId which includes members that should only be shown for this LanguageContext.
        /// 
        /// ContextId's are used for filtering by Scope's.
        /// </summary>
        public ContextId ContextId {
            get { return _id; }
        }

        /// <summary>
        /// Gets the ScriptDomainManager that this LanguageContext is running within.
        /// </summary>
        public ScriptDomainManager DomainManager {
            get { return _domainManager; }
        }

        /// <summary>
        /// Whether the language can parse code and create source units.
        /// </summary>
        public virtual bool CanCreateSourceCode {
            get { return true; }
        }

        #region Scope

        public virtual Scope GetScope(string path) {
            return null;
        }

        // TODO: remove
        public ScopeExtension EnsureScopeExtension(Scope scope) {
            ContractUtils.RequiresNotNull(scope, "scope");
            ScopeExtension extension = scope.GetExtension(ContextId);

            if (extension == null) {
                extension = CreateScopeExtension(scope);
                if (extension == null) {
                    throw Error.MustReturnScopeExtension();
                }
                return scope.SetExtension(ContextId, extension);
            }

            return extension;
        }

        // TODO: remove
        public virtual ScopeExtension CreateScopeExtension(Scope scope) {
            return new ScopeExtension(scope);
        }

        #endregion

        #region Source Code Parsing & Compilation

        /// <summary>
        /// Provides a text reader for source code that is to be read from a given stream.
        /// </summary>
        /// <param name="stream">The stream open for reading. The stream must also allow seeking.</param>
        /// <param name="defaultEncoding">An encoding that should be used if the stream doesn't have Unicode or language specific preamble.</param>
        /// <param name="path">the path of the source unit if available</param>
        /// <returns>The reader.</returns>
        /// <exception cref="IOException">An I/O error occurs.</exception>
        public virtual SourceCodeReader GetSourceReader(Stream stream, Encoding defaultEncoding, string path) {
            ContractUtils.RequiresNotNull(stream, "stream");
            ContractUtils.RequiresNotNull(defaultEncoding, "defaultEncoding");
            ContractUtils.Requires(stream.CanRead && stream.CanSeek, "stream", "The stream must support reading and seeking");

            var result = new StreamReader(stream, defaultEncoding, true);
            result.Peek();
            return new SourceCodeReader(result, result.CurrentEncoding);
        }

        /// <summary>
        /// Creates the language specific CompilerOptions object for compilation of code not bound to any particular scope.
        /// The language should flow any relevant options from LanguageContext to the newly created options instance.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate")]
        public virtual CompilerOptions GetCompilerOptions() {
            return new CompilerOptions();
        }

        /// <summary>
        /// Creates the language specific CompilerOptions object for compilation of code bound to a given scope.
        /// </summary>
        public virtual CompilerOptions GetCompilerOptions(Scope scope) {
            return GetCompilerOptions();
        }

        /// <summary>
        /// Parses the source code within a specified compiler context. 
        /// The source unit to parse is held on by the context.
        /// </summary>
        /// <returns><b>null</b> on failure.</returns>
        /// <remarks>Could also set the code properties and line/file mappings on the source unit.</remarks>
        internal protected abstract ScriptCode CompileSourceCode(SourceUnit sourceUnit, CompilerOptions options, ErrorSink errorSink);

        internal protected virtual ScriptCode LoadCompiledCode(Delegate method, string path, string customData) {
            throw new NotSupportedException();
        }

        #endregion

        #region ScriptEngine API

        public virtual Version LanguageVersion {
            get {
                return new Version(0, 0);
            }
        }

        public virtual void SetSearchPaths(ICollection<string> paths) {
            throw new NotSupportedException();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate")]
        public virtual ICollection<string> GetSearchPaths() {
            return Options.SearchPaths;
        }

#if !SILVERLIGHT
        // Convert a CodeDom to source code, and output the generated code and the line number mappings (if any)
        public virtual SourceUnit GenerateSourceCode(System.CodeDom.CodeObject codeDom, string path, SourceCodeKind kind) {
            throw new NotImplementedException();
        }
#endif

        public virtual TService GetService<TService>(params object[] args) where TService : class {
            return null;
        }

        //TODO these three properties should become abstract and updated for all implementations
        public virtual Guid LanguageGuid {
            get {
                return Guid.Empty;
            }
        }

        public virtual Guid VendorGuid {
            get {
                return Guid.Empty;
            }
        }

        public virtual void Shutdown() {
        }

        public virtual string FormatException(Exception exception) {
            return exception.ToString();
        }

        public virtual Microsoft.Scripting.LanguageOptions Options {
            get {
                return new Microsoft.Scripting.LanguageOptions();
            }
        }

        #region Source Units

        public SourceUnit CreateSnippet(string code, SourceCodeKind kind) {
            return CreateSnippet(code, null, kind);
        }

        public SourceUnit CreateSnippet(string code, string id, SourceCodeKind kind) {
            ContractUtils.RequiresNotNull(code, "code");

            return CreateSourceUnit(new SourceStringContentProvider(code), id, kind);
        }

        public SourceUnit CreateFileUnit(string path) {
            return CreateFileUnit(path, StringUtils.DefaultEncoding);
        }

        public SourceUnit CreateFileUnit(string path, Encoding encoding) {
            return CreateFileUnit(path, encoding, SourceCodeKind.File);
        }

        public SourceUnit CreateFileUnit(string path, Encoding encoding, SourceCodeKind kind) {
            ContractUtils.RequiresNotNull(path, "path");
            ContractUtils.RequiresNotNull(encoding, "encoding");

            TextContentProvider provider = new LanguageBoundTextContentProvider(this, new FileStreamContentProvider(DomainManager.Platform, path), encoding, path);
            return CreateSourceUnit(provider, path, kind);
        }

        public SourceUnit CreateFileUnit(string path, string content) {
            ContractUtils.RequiresNotNull(path, "path");
            ContractUtils.RequiresNotNull(content, "content");

            TextContentProvider provider = new SourceStringContentProvider(content);
            return CreateSourceUnit(provider, path, SourceCodeKind.File);
        }

        public SourceUnit CreateSourceUnit(StreamContentProvider contentProvider, string path, Encoding encoding, SourceCodeKind kind) {
            ContractUtils.RequiresNotNull(contentProvider, "contentProvider");
            ContractUtils.RequiresNotNull(encoding, "encoding");
            ContractUtils.Requires(path == null || path.Length > 0, "path", Strings.EmptyStringIsInvalidPath);
            ContractUtils.Requires(kind.IsValid(), "kind");
            ContractUtils.Requires(CanCreateSourceCode);

            return new SourceUnit(this, new LanguageBoundTextContentProvider(this, contentProvider, encoding, path), path, kind);
        }

        public SourceUnit CreateSourceUnit(TextContentProvider contentProvider, string path, SourceCodeKind kind) {
            ContractUtils.RequiresNotNull(contentProvider, "contentProvider");
            ContractUtils.Requires(path == null || path.Length > 0, "path", Strings.EmptyStringIsInvalidPath);
            ContractUtils.Requires(kind.IsValid(), "kind");
            ContractUtils.Requires(CanCreateSourceCode);

            return new SourceUnit(this, contentProvider, path, kind);
        }

        #endregion

        #endregion

        private static T GetArg<T>(object[] arg, int index, bool optional) {
            if (!optional && index >= arg.Length) {
                throw Error.InvalidParamNumForService();
            }

            if (!(arg[index] is T)) {
                throw Error.InvalidArgumentType(String.Format("arg[{0}]", index), typeof(T));
            }

            return (T)arg[index];
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate")]
        public virtual ErrorSink GetCompilerErrorSink() {
            return ErrorSink.Null;
        }

        public virtual void GetExceptionMessage(Exception exception, out string message, out string errorTypeName) {
            message = exception.Message;
            errorTypeName = exception.GetType().Name;
        }

        public virtual int ExecuteProgram(SourceUnit program) {
            ContractUtils.RequiresNotNull(program, "program");

            object returnValue = program.Execute();
            
            if (returnValue == null) {
                return 0;
            }

            CallSite<Func<CallSite, object, int>> site =
                CallSite<Func<CallSite, object, int>>.Create(CreateConvertBinder(typeof(int), true));

            return site.Target(site, returnValue);
        }

        #region Object Operations Support

        internal static DynamicMetaObject ErrorMetaObject(Type resultType, DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion) {
            return errorSuggestion ?? new DynamicMetaObject(
                Expression.Throw(Expression.New(typeof(NotImplementedException)), resultType),
                target.Restrictions.Merge(BindingRestrictions.Combine(args))
            );
        }

        public virtual UnaryOperationBinder CreateUnaryOperationBinder(ExpressionType operation) {
            return new DefaultUnaryOperationBinder(operation);
        }

        private sealed class DefaultUnaryOperationBinder : UnaryOperationBinder {
            internal DefaultUnaryOperationBinder(ExpressionType operation)
                : base(operation) {
            }

            public override DynamicMetaObject FallbackUnaryOperation(DynamicMetaObject target, DynamicMetaObject errorSuggestion) {
                return ErrorMetaObject(ReturnType, target, new[] { target }, errorSuggestion);
            }
        }

        public virtual BinaryOperationBinder CreateBinaryOperationBinder(ExpressionType operation) {
            return new DefaultBinaryOperationBinder(operation);
        }

        private sealed class DefaultBinaryOperationBinder : BinaryOperationBinder {
            internal DefaultBinaryOperationBinder(ExpressionType operation)
                : base(operation) {
            }

            public override DynamicMetaObject FallbackBinaryOperation(DynamicMetaObject target, DynamicMetaObject arg, DynamicMetaObject errorSuggestion) {
                return ErrorMetaObject(ReturnType, target, new[] { target, arg }, errorSuggestion);
            }
        }

        [Obsolete("Use UnaryOperation or BinaryOperation")]
        private class DefaultOperationAction : OperationBinder {
            internal DefaultOperationAction(string operation)
                : base(operation) {
            }

            public override DynamicMetaObject FallbackOperation(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion) {
                return ErrorMetaObject(ReturnType, target, args, errorSuggestion);
            }
        }

        [Obsolete("Use UnaryOperation or BinaryOperation")]
        public virtual OperationBinder CreateOperationBinder(string operation) {
            return new DefaultOperationAction(operation);
        }

        private class DefaultConvertAction : ConvertBinder {
            internal DefaultConvertAction(Type type, bool @explicit)
                : base(type, @explicit) {
            }

            public override DynamicMetaObject FallbackConvert(DynamicMetaObject self, DynamicMetaObject errorSuggestion) {
                if (Type.IsAssignableFrom(self.GetLimitType())) {
                    return new DynamicMetaObject(
                        AstUtils.Convert(self.Expression, Type),
                        BindingRestrictionsHelpers.GetRuntimeTypeRestriction(self.Expression, self.GetLimitType())
                    );
                }

                if (errorSuggestion != null) {
                    return errorSuggestion;
                }

                return new DynamicMetaObject(
                    Expression.Throw(
                        Expression.Constant(
                            new ArgumentTypeException(string.Format("Expected {0}, got {1}", Type.FullName, self.GetLimitType().FullName))
                        ),
                        ReturnType
                    ),
                    BindingRestrictionsHelpers.GetRuntimeTypeRestriction(self.Expression, self.GetLimitType())
                );
            }
        }

        public virtual ConvertBinder CreateConvertBinder(Type toType, bool explicitCast) {
            return new DefaultConvertAction(toType, explicitCast);
        }

        private class DefaultGetMemberAction : GetMemberBinder {
            internal DefaultGetMemberAction(string name, bool ignoreCase)
                : base(name, ignoreCase) {
            }

            public override DynamicMetaObject FallbackGetMember(DynamicMetaObject self, DynamicMetaObject errorSuggestion) {
                return ErrorMetaObject(ReturnType, self, DynamicMetaObject.EmptyMetaObjects, errorSuggestion);
            }
        }

        public virtual GetMemberBinder CreateGetMemberBinder(string name, bool ignoreCase) {
            return new DefaultGetMemberAction(name, ignoreCase);
        }

        private class DefaultSetMemberAction : SetMemberBinder {
            internal DefaultSetMemberAction(string name, bool ignoreCase)
                : base(name, ignoreCase) {
            }

            public override DynamicMetaObject FallbackSetMember(DynamicMetaObject self, DynamicMetaObject value, DynamicMetaObject errorSuggestion) {
                return ErrorMetaObject(ReturnType, self, new DynamicMetaObject[] { value }, errorSuggestion);
            }
        }

        public virtual SetMemberBinder CreateSetMemberBinder(string name, bool ignoreCase) {
            return new DefaultSetMemberAction(name, ignoreCase);
        }

        private class DefaultDeleteMemberAction : DeleteMemberBinder {
            internal DefaultDeleteMemberAction(string name, bool ignoreCase)
                : base(name, ignoreCase) {
            }

            public override DynamicMetaObject FallbackDeleteMember(DynamicMetaObject self, DynamicMetaObject errorSuggestion) {
                return ErrorMetaObject(ReturnType, self, DynamicMetaObject.EmptyMetaObjects, errorSuggestion);
            }
        }

        public virtual DeleteMemberBinder CreateDeleteMemberBinder(string name, bool ignoreCase) {
            return new DefaultDeleteMemberAction(name, ignoreCase);
        }

        private class DefaultCallAction : InvokeMemberBinder {
            private LanguageContext _context;

            internal DefaultCallAction(LanguageContext context, string name, bool ignoreCase, CallInfo callInfo)
                : base(name, ignoreCase, callInfo) {
                _context = context;
            }

            public override DynamicMetaObject FallbackInvokeMember(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion) {
                return ErrorMetaObject(ReturnType, target, args.AddFirst(target), errorSuggestion);
            }

            private static Expression[] GetArgs(DynamicMetaObject target, DynamicMetaObject[] args) {
                Expression[] res = new Expression[args.Length + 1];
                res[0] = target.Expression;
                for (int i = 0; i < args.Length; i++) {
                    res[1 + i] = args[i].Expression;
                }

                return res;
            }

            public override DynamicMetaObject FallbackInvoke(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion) {
                return new DynamicMetaObject(
                    Expression.Dynamic(
                        _context.CreateInvokeBinder(CallInfo),
                        typeof(object),
                        GetArgs(target, args)
                    ),
                    target.Restrictions.Merge(BindingRestrictions.Combine(args))
                );
            }
        }

        public virtual InvokeMemberBinder CreateCallBinder(string name, bool ignoreCase, CallInfo callInfo) {
            return new DefaultCallAction(this, name, ignoreCase, callInfo);
        }

        private class DefaultInvokeAction : InvokeBinder {
            internal DefaultInvokeAction(CallInfo callInfo)
                : base(callInfo) {
            }

            public override DynamicMetaObject FallbackInvoke(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion) {
                return ErrorMetaObject(ReturnType, target, args, errorSuggestion);
            }
        }

        public virtual InvokeBinder CreateInvokeBinder(CallInfo callInfo) {
            return new DefaultInvokeAction(callInfo);
        }

        private class DefaultCreateAction : CreateInstanceBinder {
            internal DefaultCreateAction(CallInfo callInfo)
                : base(callInfo) {
            }

            public override DynamicMetaObject FallbackCreateInstance(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion) {
                return ErrorMetaObject(ReturnType, target, args, errorSuggestion);
            }
        }

        public virtual CreateInstanceBinder CreateCreateBinder(CallInfo callInfo) {
            return new DefaultCreateAction(callInfo);
        }      

        #endregion

        #region CreateDelegate support

        /// <summary> Table of dynamically generated delegates which are shared based upon method signature. </summary>
        private Publisher<DelegateSignatureInfo, DelegateInfo> _dynamicDelegateCache = new Publisher<DelegateSignatureInfo, DelegateInfo>();

        public T CreateDelegate<T>(object callable) {
            return (T)(object)GetDelegate(callable, typeof(T));
        }

        /// <summary>
        /// Creates a delegate with a given signature that could be used to invoke this object from non-dynamic code (w/o code context).
        /// A stub is created that makes appropriate conversions/boxing and calls the object.
        /// The stub should be executed within a context of this object's language.
        /// </summary>
        /// <returns>The delegate or a <c>null</c> reference if the object is not callable.</returns>
        public Delegate GetDelegate(object callableObject, Type delegateType) {
            ContractUtils.RequiresNotNull(delegateType, "delegateType");

            Delegate result = callableObject as Delegate;
            if (result != null) {
                if (!delegateType.IsAssignableFrom(result.GetType())) {
                    throw ScriptingRuntimeHelpers.SimpleTypeError(String.Format("Cannot cast {0} to {1}.", result.GetType(), delegateType));
                }

                return result;
            }

            IDynamicMetaObjectProvider dynamicObject = callableObject as IDynamicMetaObjectProvider;
            if (dynamicObject != null) {

                MethodInfo invoke;

                if (!typeof(Delegate).IsAssignableFrom(delegateType) || (invoke = delegateType.GetMethod("Invoke")) == null) {
                    throw ScriptingRuntimeHelpers.SimpleTypeError("A specific delegate type is required.");
                }

                ParameterInfo[] parameters = invoke.GetParameters();
                DelegateSignatureInfo signatureInfo = new DelegateSignatureInfo(
                    invoke.ReturnType,
                    parameters
                );

                DelegateInfo delegateInfo = _dynamicDelegateCache.GetOrCreateValue(signatureInfo,
                    delegate() {
                        // creation code
                        return signatureInfo.GenerateDelegateStub(this);
                    });


                result = delegateInfo.CreateDelegate(delegateType, dynamicObject);
                if (result != null) {
                    return result;
                }
            }

            throw ScriptingRuntimeHelpers.SimpleTypeError("Object is not callable.");
        }

        #endregion

        /// <summary>
        /// Gets the member names associated with the object
        /// By default, only returns IDO names
        /// </summary>
        internal protected virtual IList<string> GetMemberNames(object obj) {
            var ido = obj as IDynamicMetaObjectProvider;
            if (ido != null) {
                var mo = ido.GetMetaObject(Expression.Parameter(typeof(object), null));
                return mo.GetDynamicMemberNames().ToReadOnly();
            }
            return EmptyArray<string>.Instance;
        }

        public virtual string GetDocumentation(object obj) {
            return String.Empty;
        }

        public virtual IList<string> GetCallSignatures(object obj) {
            return new string[0];
        }

        public virtual bool IsCallable(object obj) {
            if (obj == null) {
                return false;
            }

            return typeof(Delegate).IsAssignableFrom(obj.GetType());
        }

        /// <summary>
        /// Returns a string representation of the object in a language specific object display format.
        /// </summary>
        /// <param name="operations">Dynamic sites container that could be used for any dynamic dispatches necessary for formatting.</param>
        /// <param name="obj">Object to format.</param>
        /// <returns>A string representation of object.</returns>
        internal protected virtual string FormatObject(DynamicOperations operations, object obj) {
            return obj == null ? "null" : obj.ToString();
        }
    }
}
