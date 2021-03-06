/*
 * Copyright (C) 2008, Shawn O. Pearce <spearce@spearce.org>
 * Copyright (C) 2009, Henon <meinrad.recheis@gmail.com>
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
using GitSharp.RevWalk.Filter;
using GitSharp.TreeWalk.Filter;

namespace GitSharp.RevWalk
{



    /**
     * Initial RevWalk generator that bootstraps a new walk.
     * <p>
     * Initially RevWalk starts with this generator as its chosen implementation.
     * The first request for a RevCommit from the RevWalk instance calls to our
     * {@link #next()} method, and we replace ourselves with the best Generator
     * implementation available based upon the current RevWalk configuration.
     */
    public class StartGenerator : Generator
    {
        private RevWalk walker;

        public StartGenerator(RevWalk w)
        {
            walker = w;
        }

        public override int outputType()
        {
            return 0;
        }

        public override RevCommit next()
        {
            Generator g;

            RevWalk w = walker;
            RevFilter rf = w.getRevFilter();
            TreeFilter tf = w.getTreeFilter();
            AbstractRevQueue q = walker.queue;

            if (rf == RevFilter.MERGE_BASE)
            {
                // Computing for merge bases is a special case and does not
                // use the bulk of the generator pipeline.
                //
                if (tf != TreeFilter.ALL)
                    throw new InvalidOperationException("Cannot combine TreeFilter " + tf + " with RevFilter " + rf + ".");

                MergeBaseGenerator mbg = new MergeBaseGenerator(w);
                walker.pending = mbg;
                walker.queue = AbstractRevQueue.EMPTY_QUEUE;
                mbg.init(q);
                return mbg.next();
            }

            bool uninteresting = q.anybodyHasFlag(RevWalk.UNINTERESTING);
            bool boundary = walker.hasRevSort(RevSort.BOUNDARY);

            if (!boundary && walker is ObjectWalk)
            {
                // The object walker requires boundary support to color
                // trees and blobs at the boundary uninteresting so it
                // does not produce those in the result.
                //
                boundary = true;
            }
            if (boundary && !uninteresting)
            {
                // If we were not fed uninteresting commits we will never
                // construct a boundary. There is no reason to include the
                // extra overhead associated with that in our pipeline.
                //
                boundary = false;
            }

            DateRevQueue pending;
            int pendingOutputType = 0;
            if (q is DateRevQueue)
                pending = (DateRevQueue)q;
            else
                pending = new DateRevQueue(q);
            if (tf != TreeFilter.ALL)
            {
                rf = AndRevFilter.create(rf, new RewriteTreeFilter(w, tf));
                pendingOutputType |= HAS_REWRITE | NEEDS_REWRITE;
            }

            walker.queue = q;
            g = new PendingGenerator(w, pending, rf, pendingOutputType);

            if (boundary)
            {
                // Because the boundary generator may produce uninteresting
                // commits we cannot allow the pending generator to dispose
                // of them early.
                //
                ((PendingGenerator)g).canDispose = false;
            }

            if ((g.outputType() & NEEDS_REWRITE) != 0)
            {
                // Correction for an upstream NEEDS_REWRITE is to buffer
                // fully and then apply a rewrite generator that can
                // pull through the rewrite chain and produce a dense
                // output graph.
                //
                g = new FIFORevQueue(g);
                g = new RewriteGenerator(g);
            }

            if (walker.hasRevSort(RevSort.TOPO)
                    && (g.outputType() & SORT_TOPO) == 0)
                g = new TopoSortGenerator(g);
            if (walker.hasRevSort(RevSort.REVERSE))
                g = new LIFORevQueue(g);
            if (boundary)
                g = new BoundaryGenerator(w, g);
            else if (uninteresting)
            {
                // Try to protect ourselves from uninteresting commits producing
                // due to clock skew in the commit time stamps. Delay such that
                // we have a chance at coloring enough of the graph correctly,
                // and then strip any UNINTERESTING nodes that may have leaked
                // through early.
                //
                if (pending.peek() != null)
                    g = new DelayRevQueue(g);
                g = new FixUninterestingGenerator(g);
            }

            w.pending = g;
            return g.next();
        }
    }
}
