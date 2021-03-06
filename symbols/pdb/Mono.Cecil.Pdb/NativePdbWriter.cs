//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.
//

using System;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;

using Mono.Cecil.Cil;
using Mono.Collections.Generic;

#if !READ_ONLY

namespace Mono.Cecil.Pdb {

	public class NativePdbWriter : Cil.ISymbolWriter {

		readonly ModuleDefinition module;
		readonly SymWriter writer;
		readonly Dictionary<string, SymDocumentWriter> documents;

		internal NativePdbWriter (ModuleDefinition module, SymWriter writer)
		{
			this.module = module;
			this.writer = writer;
			this.documents = new Dictionary<string, SymDocumentWriter> ();
		}

		public ImageDebugHeader GetDebugHeader ()
		{
			ImageDebugDirectory directory;
			var data = writer.GetDebugInfo (out directory);
			directory.TimeDateStamp = (int) module.timestamp;
			return new ImageDebugHeader (new ImageDebugHeaderEntry (directory, data));
		}

		public void Write (MethodDebugInformation info)
		{
			var method_token = info.method.MetadataToken;
			var sym_token = new SymbolToken (method_token.ToInt32 ());

			writer.OpenMethod (sym_token);

			if (!info.sequence_points.IsNullOrEmpty ())
				DefineSequencePoints (info.sequence_points);

			if (info.scope != null)
				DefineScope (info.scope, info);

			writer.CloseMethod ();
		}

		void DefineScope (ScopeDebugInformation scope, MethodDebugInformation info)
		{
			var start_offset = scope.Start.Offset;
			var end_offset = scope.End.IsEndOfMethod
				? info.code_size
				: scope.End.Offset;

			writer.OpenScope (start_offset);

			var sym_token = new SymbolToken (info.local_var_token.ToInt32 ());

			if (!scope.variables.IsNullOrEmpty ()) {
				for (int i = 0; i < scope.variables.Count; i++) {
					var variable = scope.variables [i];
					CreateLocalVariable (variable, sym_token, start_offset, end_offset);
				}
			}

			if (!scope.scopes.IsNullOrEmpty ()) {
				for (int i = 0; i < scope.scopes.Count; i++)
					DefineScope (scope.scopes [i], info);
			}

			writer.CloseScope (end_offset);
		}

		void DefineSequencePoints (Collection<SequencePoint> sequence_points)
		{
			for (int i = 0; i < sequence_points.Count; i++) {
				var sequence_point = sequence_points [i];

				writer.DefineSequencePoints (
					GetDocument (sequence_point.Document),
					new [] { sequence_point.Offset },
					new [] { sequence_point.StartLine },
					new [] { sequence_point.StartColumn },
					new [] { sequence_point.EndLine },
					new [] { sequence_point.EndColumn });
			}
		}

		void CreateLocalVariable (VariableDebugInformation variable, SymbolToken local_var_token, int start_offset, int end_offset)
		{
			writer.DefineLocalVariable2 (
				variable.Name,
				variable.Attributes,
				local_var_token,
				SymAddressKind.ILOffset,
				variable.Index,
				0,
				0,
				start_offset,
				end_offset);
		}

		SymDocumentWriter GetDocument (Document document)
		{
			if (document == null)
				return null;

			SymDocumentWriter doc_writer;
			if (documents.TryGetValue (document.Url, out doc_writer))
				return doc_writer;

			doc_writer = writer.DefineDocument (
				document.Url,
				document.Language.ToGuid (),
				document.LanguageVendor.ToGuid (),
				document.Type.ToGuid ());

			documents [document.Url] = doc_writer;
			return doc_writer;
		}

		public void Dispose ()
		{
			var entry_point = module.EntryPoint;
			if (entry_point != null)
				writer.SetUserEntryPoint (new SymbolToken (entry_point.MetadataToken.ToInt32 ()));

			writer.Close ();
		}
	}
}

#endif
