namespace NetworkTools.Systems.Tools.Connect {
    using NetworkTools.Systems.Tools.Parameters;

    /// <summary>
    ///     Selects which of the tangent-circle solutions the loop generator uses.
    ///     <para>
    ///         A circle tangent to both node tangents has up to four solutions -- one per region formed
    ///         by the two tangent lines. Only two of them are ever interesting, and which one you get
    ///         is decided by <b>where the centre sits relative to the corner the two leads enclose</b>:
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             <see cref="Orthogonal" /> -- centre inside that corner (the wedge the two leads
    ///             form). The arc then cuts the corner, so a right-angle junction gets a 90° fillet
    ///             and, in general, a turn of <c>t</c> degrees gets a <c>t</c> degree arc. This is the
    ///             ordinary orthogonal connection.
    ///         </item>
    ///         <item>
    ///             <see cref="Crossing" /> -- centre in the opposite (reflex) corner. The arc has to
    ///             go the long way round, so a 90° junction gets a 270° loop: the road overshoots the
    ///             junction, loops around and comes back. This is the crossing trumpet / cloverleaf.
    ///         </item>
    ///     </list>
    ///     <para>
    ///         Both solutions always exist as a mirror pair and their sweeps add up to 360°, so the two
    ///         cases can be told apart purely by the arc's sweep: <c>|sweep| &lt;= 180°</c> is
    ///         orthogonal, <c>&gt; 180°</c> is crossing. That is exactly what the generator keys on, so
    ///         there is no per-quadrant parameter to get wrong.
    ///     </para>
    ///     <para>
    ///         The two remaining shapes serve the case the first pair cannot: <b>parallel roads</b> that
    ///         sit side by side. Both describe the <i>same</i> node pair -- the link leaves START one way
    ///         and reaches END travelling the other way, because each node is the far end of its own
    ///         road -- and differ only in topology: <see cref="UTurn" /> is one 180° half circle whose
    ///         radius is fixed by the gap, <see cref="Stacked" /> is two 270° loops joined by an elevated
    ///         deck with a free radius. Each shape is only built for the geometry that fits it -- a shape
    ///         that does not apply simply produces no geometry: no preview, no error, never a silent swap.
    ///     </para>
    ///     <para>
    ///         Degenerate node pairs (coincident nodes, zero tangents) are skipped the same way.
    ///     </para>
    /// </summary>
    public enum LoopShape {
        /// <summary>Centre inside the corner: the arc cuts it -- 90° fillet on a right-angle junction.</summary>
        [EnumOption("NetworkTools.UI.Connect.LoopShapeOrthogonal", "coui://nt/ArcShape/Orthogonal.svg")]
        Orthogonal = 0,

        /// <summary>Centre in the opposite corner: the arc goes the long way -- 270° trumpet.</summary>
        [EnumOption("NetworkTools.UI.Connect.LoopShapeCrossing", "coui://nt/ArcShape/Crossing.svg")]
        Crossing = 1,

        /// <summary>
        ///     Parallel side-by-side roads, taken the tight way: one 180° semicircle. The radius is not a
        ///     free parameter here -- with both nodes pinned, the circle is uniquely <c>|AB| / 2</c> and
        ///     sits exactly between the two roads.
        /// </summary>
        [EnumOption("NetworkTools.UI.Connect.LoopShapeUTurn", "coui://nt/ArcShape/UTurn.svg")]
        UTurn = 2,

        /// <summary>
        ///     Parallel side-by-side roads, taken the long way: two 270° loops back to back, joined
        ///     across the gap by a level elevated deck. The loops are the ramps -- they carry the
        ///     height, the deck is the flyover -- and the radius is free, so the ramps can always be
        ///     made long enough for the design grade.
        /// </summary>
        [EnumOption("NetworkTools.UI.Connect.LoopShapeStacked", "coui://nt/ArcShape/Stacked.svg")]
        Stacked = 3,
    }
}
