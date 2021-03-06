/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Microsoft Public License. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Microsoft Public License, please send an email to 
 * ironruby@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Microsoft Public License.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System.Dynamic;
using Microsoft.Scripting;
using MSA = System.Linq.Expressions;

namespace IronRuby.Compiler.Ast {

    /// <summary>
    /// lhs = rhs
    /// lhs op= rhs
    /// </summary>
    public abstract class AssignmentExpression : Expression {
        // "&" "|", null, etc
        private string _operation;

        public string Operation {
            get { return _operation; }
            internal set { _operation = value; }
        }

        public AssignmentExpression(string operation, SourceSpan location)
            : base(location) {

            _operation = operation;
        }

        internal override MSA.Expression TransformDefinedCondition(AstGenerator/*!*/ gen) {
            return null;
        }

        internal override string/*!*/ GetNodeName(AstGenerator/*!*/ gen) {
            return "assignment";
        }
    }
}
