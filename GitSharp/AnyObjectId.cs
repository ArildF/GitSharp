﻿/*
 * Copyright (C) 2008, Shawn O. Pearce <spearce@spearce.org>
 * Copyright (C) 2008, Kevin Thompson <kevin.thompson@theautomaters.com>
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GitSharp.Util;
using System.IO;

namespace GitSharp
{
    [Complete]
    public abstract class AnyObjectId
        :
#if !__MonoCS__
 IComparable<ObjectId>,
#endif
 IComparable
    {

        public static readonly int ObjectIdLength = 20;
        public static readonly int StringLength = ObjectIdLength * 2;


        public static bool operator ==(AnyObjectId a, AnyObjectId b)
        {
            if ((object)a == null)
                return (object)b == null;
            else if ((object)b == null)
                return false;

            return (a.W2 == b.W2) &&
                   (a.W3 == b.W3) &&
                   (a.W4 == b.W4) &&
                   (a.W5 == b.W5) &&
                   (a.W1 == b.W1);
        }

        public static bool operator !=(AnyObjectId a, AnyObjectId b)
        {
            return !(a == b);
        }

        public virtual bool Equals(AnyObjectId obj)
        {
            return (obj != null) ? this == obj : false;
        }

        public override bool Equals(object obj)
        {
            return Equals(((AnyObjectId)obj));
        }

        public void CopyTo(Stream s)
        {
            new BinaryWriter(s).Write(ToHexByteArray());
        }

        /**
         * Copy this ObjectId to a StringBuilder in hex format.
         *
         * @param tmp
         *            temporary char array to buffer construct into before writing.
         *            Must be at least large enough to hold 2 digits for each byte
         *            of object id (40 characters or larger).
         * @param w
         *            the string to append onto.
         */
        public void CopyTo(char[] tmp, StringBuilder w)
        {
            ToHexCharArray(tmp);
            w.Append(tmp, 0, StringLength);
        }


        internal void copyRawTo(Stream s)
        {
            var buf = new byte[20];
            NB.encodeInt32(buf, 0, W1);
            NB.encodeInt32(buf, 4, W2);
            NB.encodeInt32(buf, 8, W3);
            NB.encodeInt32(buf, 12, W4);
            NB.encodeInt32(buf, 16, W5);
            s.Write(buf, 0, 20);
        }

        private byte[] ToHexByteArray()
        {
            byte[] dst = new byte[StringLength];

            Hex.FillHexByteArray(dst, 0, W1);
            Hex.FillHexByteArray(dst, 8, W2);
            Hex.FillHexByteArray(dst, 16, W3);
            Hex.FillHexByteArray(dst, 24, W4);
            Hex.FillHexByteArray(dst, 32, W5);
            return dst;
        }

        public override int GetHashCode()
        {
            return (int)this.W2;
        }

        public AbbreviatedObjectId Abbreviate(Repository repo)
        {
            return Abbreviate(repo, 8);
        }

        public AbbreviatedObjectId Abbreviate(Repository repo, int len)
        {
            int a = AbbreviatedObjectId.Mask(len, 1, W1);
            int b = AbbreviatedObjectId.Mask(len, 2, W2);
            int c = AbbreviatedObjectId.Mask(len, 3, W3);
            int d = AbbreviatedObjectId.Mask(len, 4, W4);
            int e = AbbreviatedObjectId.Mask(len, 5, W5);
            return new AbbreviatedObjectId(len, a, b, c, d, e);
        }

        public int W1 { get; set; }
        public int W2 { get; set; }
        public int W3 { get; set; }
        public int W4 { get; set; }
        public int W5 { get; set; }

        public int GetFirstByte()
        {
            return (byte)(((uint)W1) >> 24); // W1 >>> 24 in java
        }

        #region IComparable<ObjectId> Members

        public int CompareTo(ObjectId other)
        {
            if (this == other)
                return 0;

            int cmp;

            cmp = NB.CompareUInt32(W1, other.W1);
            if (cmp != 0)
                return cmp;

            cmp = NB.CompareUInt32(W2, other.W2);
            if (cmp != 0)
                return cmp;

            cmp = NB.CompareUInt32(W3, other.W3);
            if (cmp != 0)
                return cmp;

            cmp = NB.CompareUInt32(W4, other.W4);
            if (cmp != 0)
                return cmp;

            return NB.CompareUInt32(W5, other.W5);

        }

        public int CompareTo(byte[] bs, int p)
        {
            int cmp;

            cmp = NB.CompareUInt32(W1, NB.DecodeInt32(bs, p));
            if (cmp != 0)
                return cmp;

            cmp = NB.CompareUInt32(W2, NB.DecodeInt32(bs, p + 4));
            if (cmp != 0)
                return cmp;

            cmp = NB.CompareUInt32(W3, NB.DecodeInt32(bs, p + 8));
            if (cmp != 0)
                return cmp;

            cmp = NB.CompareUInt32(W4, NB.DecodeInt32(bs, p + 12));
            if (cmp != 0)
                return cmp;

            return NB.CompareUInt32(W5, NB.DecodeInt32(bs, p + 16));
        }

        public int CompareTo(int[] bs, int p)
        {
            int cmp = NB.CompareUInt32(W1, bs[p]);
            if (cmp != 0)
                return cmp;

            cmp = NB.CompareUInt32(W2, bs[p + 1]);
            if (cmp != 0)
                return cmp;

            cmp = NB.CompareUInt32(W3, bs[p + 2]);
            if (cmp != 0)
                return cmp;

            cmp = NB.CompareUInt32(W4, bs[p + 3]);
            if (cmp != 0)
                return cmp;

            return NB.CompareUInt32(W5, bs[p + 4]);
        }


        #endregion

        #region IComparable Members

        public int CompareTo(object obj)
        {
            return this.CompareTo((ObjectId)obj);
        }

        #endregion

        /**
	     * Tests if this ObjectId starts with the given abbreviation.
	     *
	     * @param abbr
	     *            the abbreviation.
	     * @return true if this ObjectId begins with the abbreviation; else false.
	     */
        public bool startsWith(AbbreviatedObjectId abbr)
        {
            return abbr.prefixCompare(this) == 0;
        }

        private char[] ToHexCharArray()
        {
            char[] dest = new char[StringLength];
            ToHexCharArray(dest);
            return dest;
        }

        private void ToHexCharArray(char[] dest)
        {
            Hex.FillHexCharArray(dest, 0, W1);
            Hex.FillHexCharArray(dest, 8, W2);
            Hex.FillHexCharArray(dest, 16, W3);
            Hex.FillHexCharArray(dest, 24, W4);
            Hex.FillHexCharArray(dest, 32, W5);
        }

        public override string ToString()
        {
            return new string(ToHexCharArray());
        }

        public ObjectId Copy()
        {
            if (this.GetType() == typeof(ObjectId))
                return (ObjectId)this;
            return new ObjectId(this);
        }

        public abstract ObjectId ToObjectId();

    }
}
