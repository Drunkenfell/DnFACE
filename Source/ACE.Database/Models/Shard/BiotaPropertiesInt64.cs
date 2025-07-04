using System;
using System.Collections.Generic;

namespace ACE.Database.Models.Shard;

/// <summary>
/// Int64 Properties of Weenies
/// </summary>
public partial class BiotaPropertiesInt64
{
    /// <summary>
    /// Id of the object this property belongs to
    /// </summary>
    public uint ObjectId { get; set; }

    /// <summary>
    /// Type of Property the value applies to (PropertyInt64.????)
    /// </summary>
    public ushort Type { get; set; }

    /// <summary>
    /// Value of this Property
    /// </summary>
    public long Value { get; set; }

    public virtual Biota Object { get; set; }
}
