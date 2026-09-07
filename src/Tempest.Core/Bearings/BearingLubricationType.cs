namespace Tempest.Core.Bearings;

/// <summary>The lubrication a bearing is supplied or specified with.</summary>
public enum BearingLubricationType
{
    /// <summary>Not recorded.</summary>
    Unspecified,

    /// <summary>Supplied dry, to be lubricated on installation.</summary>
    Unlubricated,

    /// <summary>Grease.</summary>
    Grease,

    /// <summary>Oil.</summary>
    Oil,

    /// <summary>A solid lubricant or self-lubricating liner.</summary>
    SolidLubricant,

    /// <summary>A lubrication arrangement this vocabulary does not name; record the source's own wording in <see cref="BearingLubrication.LubricantDesignation"/>.</summary>
    Other
}
