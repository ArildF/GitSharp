﻿/*
 * Copyright (C) 2007, Dave Watson <dwatson@mimvista.com>
 * Copyright (C) 2008, Robin Rosenberg <robin.rosenberg@dewire.com>
 * Copyright (C) 2006, Shawn O. Pearce <spearce@spearce.org>
 * Copyright (C) 2008, Kevin Thompson <kevin.thompson@theautomaters.com>
 *
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or
 * without modification, are permitted provided that the following
 * conditions are met:
 *
 * - Redistributions of source code must retain the above copyright
 *   notice, this list of conditions and the following disclaimer.
 *
 * - Redistributions in binary form must reproduce the above
 *   copyright notice, this list of conditions and the following
 *   disclaimer in the documentation and/or other materials provided
 *   with the distribution.
 *
 * - Neither the name of the Git Development Community nor the
 *   names of its contributors may be used to endorse or promote
 *   products derived from this software without specific prior
 *   written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND
 * CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES,
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
 * NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
 * CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
 * STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
 * ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace GitSharp
{
    [Complete]
    public class AbstractIndexTreeVisitor : IndexTreeVisitor
    {

        public delegate void FinishVisitTreeDelegate(Tree tree, Tree auxTree, string curDir);
        public FinishVisitTreeDelegate FinishVisitTree { get; set; }

        public delegate void FinishVisitTreeByIndexDelegate(Tree tree, int i, string curDir);
        public FinishVisitTreeByIndexDelegate FinishVisitTreeByIndex { get; set; }
        
        public delegate void VisitEntryDelegate(TreeEntry treeEntry, GitIndex.Entry indexEntry, FileInfo file);
        public VisitEntryDelegate VisitEntry { get; set; }

        public delegate void VisitEntryAuxDelegate(TreeEntry treeEntry, TreeEntry auxEntry, GitIndex.Entry indexEntry, FileInfo file);
        public VisitEntryAuxDelegate VisitEntryAux { get; set; }

        #region IndexTreeVisitor Members

        void IndexTreeVisitor.VisitEntry(TreeEntry treeEntry, GitIndex.Entry indexEntry, FileInfo file)
        {
            VisitEntryDelegate handler = this.VisitEntry;
            if(handler!=null)
                handler(treeEntry, indexEntry, file);            
        }

        void IndexTreeVisitor.VisitEntry(TreeEntry treeEntry, TreeEntry auxEntry, GitIndex.Entry indexEntry, FileInfo file)
        {
            VisitEntryAuxDelegate handler = this.VisitEntryAux;
            if (handler != null)
                handler(treeEntry,auxEntry, indexEntry, file);    
        }

        void IndexTreeVisitor.FinishVisitTree(Tree tree, Tree auxTree, string curDir)
        {
            FinishVisitTreeDelegate handler = this.FinishVisitTree;
            if (handler != null)
                handler(tree, auxTree, curDir);
        }

        void IndexTreeVisitor.FinishVisitTree(Tree tree, int i, string curDir)
        {
            FinishVisitTreeByIndexDelegate handler = this.FinishVisitTreeByIndex;
            if (handler != null)
                handler(tree, i, curDir);
        }

        #endregion
    }
}
