﻿/*
 * Copyright (C) 2008, Robin Rosenberg <robin.rosenberg@dewire.com>
 * Copyright (C) 2008, Shawn O. Pearce <spearce@spearce.org>
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
using System.Text;
using System.Text.RegularExpressions;

namespace GitSharp.Transport
{
    /**
 * This URI like construct used for referencing Git archives over the net, as
 * well as locally stored archives. The most important difference compared to
 * RFC 2396 URI's is that no URI encoding/decoding ever takes place. A space or
 * any special character is written as-is.
 */
    public class URIish
    {
        private static readonly Regex FULL_URI =
            new Regex(
                "^(?:([a-z][a-z0-9+-]+)://(?:([^/]+?)(?::([^/]+?))?@)?(?:([^/]+?))?(?::(\\d+))?)?((?:[A-Za-z]:)?/.+)$");

        private static readonly Regex SCP_URI = new Regex("^(?:([^@]+?)@)?([^:]+?):(.+)$");

        public string Scheme { get; private set; }
        public string Path { get; private set; }
        public string User { get; private set; }
        public string Pass { get; private set; }
        public int Port { get; private set; }
        public string Host { get; private set; }

        /**
 * Construct a URIish from a standard URL.
 *
 * @param u
 *            the source URL to convert from.
 */
        public URIish(Uri u)
        {
            Scheme = u.Scheme;
            Path = u.AbsolutePath;
            Port = u.Port;
            Host = u.Host;

            string ui = u.UserInfo;
            if (ui != null)
            {
                int d = ui.IndexOf(':');
                User = d < 0 ? ui : ui.Substring(0, d);
                Pass = d < 0 ? null : ui.Substring(d + 1);
            }
        }

        /**
 * Parse and construct an {@link URIish} from a string
 * 
 * @param s
 * @throws URISyntaxException
 */
        public URIish(string s)
        {
            s = s.Replace('\\', '/');
            Match matcher = FULL_URI.Match(s);
            if (matcher.Success)
            {
                Scheme = matcher.Groups[1].Value;
                User = matcher.Groups[2].Value;
                Pass = matcher.Groups[3].Value;
                Host = matcher.Groups[4].Value;
                if (matcher.Groups[5].Success)
                    Port = int.Parse(matcher.Groups[5].Value);
                Path = matcher.Groups[6].Value;
                if (Path.Length >= 3 && Path[0] == '/' && Path[2] == ':' && (Path[1] >= 'A' && Path[1] <= 'Z' || Path[1] >= 'a' && Path[1] <= 'z'))
                    Path = Path.Substring(1);
            }
            else
            {
                matcher = SCP_URI.Match(s);
                if (matcher.Success)
                {
                    User = matcher.Groups[1].Value;
                    Host = matcher.Groups[2].Value;
                    Path = matcher.Groups[3].Value;
                }
                else
                {
                    throw new UriFormatException("Cannot parse Git URI-ish (" + s + ")");
                }
            }
        }

        /** Create an empty, non-configured URI. */
        public URIish()
        {
            Port = -1;
        }

        private URIish(URIish u)
        {
            Scheme = u.Scheme;
            Path = u.Path;
            User = u.User;
            Pass = u.Pass;
            Port = u.Port;
            Host = u.Host;
        }

        /**
 * @return true if this URI references a repository on another system.
 */
        public bool IsRemote
        {
            get
            {
                return Host != null;
            }
        }

        /**
 * Return a new URI matching this one, but with a different host.
 * 
 * @param n
 *            the new value for host.
 * @return a new URI with the updated value.
 */
        public URIish SetHost(string n)
        {
            return new URIish(this) { Host = n };
        }

        /**
 * Return a new URI matching this one, but with a different scheme.
 * 
 * @param n
 *            the new value for scheme.
 * @return a new URI with the updated value.
 */
        public URIish SetScheme(string n)
        {
            return new URIish(this) { Scheme = n };
        }

        /**
 * Return a new URI matching this one, but with a different path.
 * 
 * @param n
 *            the new value for path.
 * @return a new URI with the updated value.
 */
        public URIish SetPath(string n)
        {
            return new URIish(this) { Path = n };
        }

        /**
 * Return a new URI matching this one, but with a different user.
 * 
 * @param n
 *            the new value for user.
 * @return a new URI with the updated value.
 */
        public URIish SetUser(string n)
        {
            return new URIish(this) { User = n };
        }

        /**
 * Return a new URI matching this one, but with a different password.
 * 
 * @param n
 *            the new value for password.
 * @return a new URI with the updated value.
 */
        public URIish SetPass(string n)
        {
            return new URIish(this) { Pass = n };
        }

        /**
 * Return a new URI matching this one, but with a different port.
 * 
 * @param n
 *            the new value for port.
 * @return a new URI with the updated value.
 */
        public URIish SetPort(int n)
        {
            return new URIish(this) { Port = (n > 0 ? n : -1) };
        }

        public override int GetHashCode()
        {
            int hc = 0;
            if (Scheme != null)
                hc = hc * 31 + Scheme.GetHashCode();
            if (User != null)
                hc = hc * 31 + User.GetHashCode();
            if (Pass != null)
                hc = hc * 31 + Pass.GetHashCode();
            if (Host != null)
                hc = hc * 31 + Host.GetHashCode();
            if (Port > 0)
                hc = hc * 31 + Port;
            if (Path != null)
                hc = hc * 31 + Path.GetHashCode();
            return hc;
        }

        private static bool eq(string a, string b)
        {
            if (a == b) return true;
            if (a == null || b == null) return false;
            return a.Equals(b);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is URIish))
                return false;

            URIish b = (URIish)obj;
            if (!eq(Scheme, b.Scheme)) return false;
            if (!eq(User, b.User)) return false;
            if (!eq(Pass, b.Pass)) return false;
            if (!eq(Host, b.Host)) return false;
            if (Port != b.Port) return false;
            if (!eq(Path, b.Path)) return false;
            return true;
        }

        /**
 * Obtain the string form of the URI, with the password included.
 *
 * @return the URI, including its password field, if any.
 */
        public string ToPrivateString()
        {
            return format(true);
        }

        public override string ToString()
        {
            return format(false);
        }

        private string format(bool includePassword)
        {
            StringBuilder r = new StringBuilder();
            if (Scheme != null)
            {
                r.Append(Scheme);
                r.Append("://");
            }

            if (User != null)
            {
                r.Append(User);
                if (includePassword && Pass != null)
                {
                    r.Append(':');
                    r.Append(Pass);
                }
            }

            if (Host != null)
            {
                if (User != null)
                    r.Append('@');
                r.Append(Host);
                if (Scheme != null && Port > 0)
                {
                    r.Append(':');
                    r.Append(Port);
                }
            }

            if (Path != null)
            {
                if (Scheme != null)
                {
                    if (!Path.StartsWith("/"))
                        r.Append('/');
                }
                else if (Host != null)
                {
                    r.Append(':');
                }
                r.Append(Path);
            }

            return r.ToString();
        }
    }
}