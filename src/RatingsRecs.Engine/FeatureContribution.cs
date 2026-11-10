using System;
using System.Collections.Generic;

namespace RatingsRecs.Engine;

/// <summary>
/// One feature contribution of an explanation.
/// </summary>
/// <param name="Category">The category.</param>
/// <param name="Value">The display value (e.g. "Ridley Scott").</param>
/// <param name="Contribution">The contribution to the taste score.</param>
public sealed record FeatureContribution(FeatureCategory Category, string Value, double Contribution);
