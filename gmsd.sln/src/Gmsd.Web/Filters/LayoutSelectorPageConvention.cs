using Gmsd.Web.Services;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Gmsd.Web.Filters;

/// <summary>
/// Razor Pages convention that applies the LayoutSelectorFilter to all pages.
/// </summary>
public class LayoutSelectorPageConvention : IPageApplicationModelConvention
{
    /// <summary>
    /// Applies the layout selector filter to the page model.
    /// </summary>
    public void Apply(PageApplicationModel model)
    {
        // Add the LayoutSelectorFilter to all pages
        model.Filters.Add(new LayoutSelectorFilterFactory());
    }
}

/// <summary>
/// Factory for creating LayoutSelectorFilter instances with proper DI.
/// </summary>
public class LayoutSelectorFilterFactory : IFilterFactory
{
    /// <summary>
    /// Indicates whether this factory can be reused across multiple requests.
    /// </summary>
    public bool IsReusable => false;

    /// <summary>
    /// Creates a new instance of the LayoutSelectorFilter with injected services.
    /// </summary>
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        var featureFlagService = serviceProvider.GetRequiredService<IFeatureFlagService>();
        var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
        var logger = serviceProvider.GetRequiredService<ILogger<LayoutSelectorFilter>>();

        return new LayoutSelectorFilter(featureFlagService, httpContextAccessor, logger);
    }
}
