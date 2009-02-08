﻿/* ****************************************************************************
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
using System.Linq.Expressions;
using System.Dynamic;

using Microsoft.Scripting;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Types;
    
namespace IronPython.Runtime.Binding {
    using Ast = System.Linq.Expressions.Expression;

    partial class MetaPythonType : MetaPythonObject {
        public MetaPythonType(Expression/*!*/ expression, BindingRestrictions/*!*/ restrictions, PythonType/*!*/ value)
            : base(expression, BindingRestrictions.Empty, value) {
            Assert.NotNull(value);
        }

        public override DynamicMetaObject BindCreateInstance(CreateInstanceBinder create, params DynamicMetaObject[] args) {
            return InvokeWorker(create, args, Ast.Constant(BinderState.GetBinderState(create).Context));
        }

        public override DynamicMetaObject BindConvert(ConvertBinder/*!*/ conversion) {
            if (conversion.Type.IsSubclassOf(typeof(Delegate))) {
                return MakeDelegateTarget(conversion, conversion.Type, Restrict(Value.GetType()));
            }
            return conversion.FallbackConvert(this);
        }

        public override System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, object>> GetDynamicDataMembers() {
            PythonContext pc = Value.PythonContext ?? DefaultContext.DefaultPythonContext;

            IAttributesCollection dict = Value.GetMemberDictionary(pc.DefaultBinderState.Context);

            foreach (KeyValuePair<SymbolId, object> members in dict.SymbolAttributes) {
                // all members are data members in a type.
                yield return new KeyValuePair<string, object>(SymbolTable.IdToString(members.Key), members.Value);
            }
        }

        public override System.Collections.Generic.IEnumerable<string> GetDynamicMemberNames() {
            PythonContext pc = Value.PythonContext ?? DefaultContext.DefaultPythonContext;

            foreach (object o in Value.GetMemberNames(pc.DefaultBinderState.Context)) {
                if (o is string) {
                    yield return (string)o;
                }
            }
        }

        public new PythonType/*!*/ Value {
            get {
                return (PythonType)base.Value;
            }
        }
    }
}
