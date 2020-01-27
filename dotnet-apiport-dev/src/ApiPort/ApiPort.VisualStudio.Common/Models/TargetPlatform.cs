﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace ApiPortVS
{
    public class TargetPlatform : EqualityComparer<TargetPlatform>, IComparable<TargetPlatform>
    {
        private readonly ICollection<string> _alternativeNames = new HashSet<string>(StringComparer.Ordinal);

        public TargetPlatform()
        {
            Versions = Array.Empty<TargetPlatformVersion>();
        }

        public string Name { get; set; }

        public ICollection<TargetPlatformVersion> Versions { get; set; }

        public ICollection<string> AlternativeNames { get { return _alternativeNames; } }

        public override bool Equals(object obj)
        {
            if (!(obj is TargetPlatform compared))
            {
                return false;
            }

            return string.Equals(Name, compared.Name, StringComparison.Ordinal)
                && Versions.SequenceEqual(compared.Versions);
        }

        public override int GetHashCode()
        {
            const int HashMultipler = 31;

            unchecked
            {
                int hash = 17;

                if (Name != null)
                {
                    hash = (hash * HashMultipler) + Name.GetHashCode();
                }

                if (Versions != null)
                {
                    foreach (var version in Versions)
                    {
                        hash = (hash * HashMultipler) + version.GetHashCode();
                    }
                }

                return hash;
            }
        }

        public override string ToString()
        {
            return Name;
        }

        public override bool Equals(TargetPlatform x, TargetPlatform y)
        {
            if (x == null && y != null)
            {
                return false;
            }

            return x.Equals(y);
        }

        public override int GetHashCode(TargetPlatform obj)
        {
            return obj.GetHashCode();
        }

        public int CompareTo(TargetPlatform other)
        {
            if (other == null)
            {
                return -1;
            }
            else if (Equals(other))
            {
                return 0;
            }

            var comparedNames = string.CompareOrdinal(Name, other.Name);

            if (comparedNames != 0)
            {
                return comparedNames;
            }

            // Adapted from:
            // https://github.com/dotnet/corefx/blob/master/src/System.Linq/src/System/Linq/SequenceEqual.cs#L43-L55
            // Remarks:
            // We opted to use this logic instead of calling
            // `e1.SequenceEquals(e2)` because SequenceEquals only tells us
            // whether or not to return 0.  CompareTo needs to return more
            // information, like whether e1 comes before e2 (returning -1) or e1
            // comes after e2 (returning +1).
            using (var e1 = Versions.GetEnumerator())
            {
                using (var e2 = other.Versions.GetEnumerator())
                {
                    while (e1.MoveNext())
                    {
                        // `this` has more Versions than the compared object, so
                        // `this` should come after the compared object since all
                        // other elements up until this point were equal.
                        if (!e2.MoveNext())
                        {
                            return 1;
                        }
                        else if (Equals(e1.Current, e2.Current))
                        {
                            continue;
                        }
                        else
                        {
                            return e1.Current.Version.CompareTo(e2.Current.Version);
                        }
                    }

                    // Compared has more Versions than `this`.  `this` comes first
                    if (e2.MoveNext())
                    {
                        return -1;
                    }

                    return 0;
                }
            }
        }
    }
}
