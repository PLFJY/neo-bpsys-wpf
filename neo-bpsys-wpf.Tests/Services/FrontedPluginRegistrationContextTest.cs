using neo_bpsys_wpf.Core.Services.Registry;
using System;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public class FrontedPluginRegistrationContextTest
{
    [Fact]
    public void NestedScopes_RestorePreviousValueOnDispose()
    {
        Assert.Null(FrontedPluginRegistrationContext.CurrentPackageId);

        using (FrontedPluginRegistrationContext.BeginScope("a"))
        {
            Assert.Equal("a", FrontedPluginRegistrationContext.CurrentPackageId);

            using (FrontedPluginRegistrationContext.BeginScope("b"))
            {
                Assert.Equal("b", FrontedPluginRegistrationContext.CurrentPackageId);
            }

            Assert.Equal("a", FrontedPluginRegistrationContext.CurrentPackageId);
        }

        Assert.Null(FrontedPluginRegistrationContext.CurrentPackageId);
    }

    [Fact]
    public void ExceptionInsideScope_RestoresOuterValueAfterUsing()
    {
        Assert.Null(FrontedPluginRegistrationContext.CurrentPackageId);

        using (FrontedPluginRegistrationContext.BeginScope("outer"))
        {
            try
            {
                using (FrontedPluginRegistrationContext.BeginScope("inner"))
                {
                    Assert.Equal("inner", FrontedPluginRegistrationContext.CurrentPackageId);
                    throw new InvalidOperationException("boom");
                }
            }
            catch (InvalidOperationException)
            {
                // 异常被 using 捕获并触发 Dispose，内层值应已恢复为外层。
            }

            Assert.Equal("outer", FrontedPluginRegistrationContext.CurrentPackageId);
        }

        Assert.Null(FrontedPluginRegistrationContext.CurrentPackageId);
    }

    [Fact]
    public void SequentialPluginScopes_DoNotLeakBetweenInitializations()
    {
        Assert.Null(FrontedPluginRegistrationContext.CurrentPackageId);

        using (FrontedPluginRegistrationContext.BeginScope("a"))
        {
            Assert.Equal("a", FrontedPluginRegistrationContext.CurrentPackageId);
        }

        Assert.Null(FrontedPluginRegistrationContext.CurrentPackageId);

        using (FrontedPluginRegistrationContext.BeginScope("b"))
        {
            Assert.Equal("b", FrontedPluginRegistrationContext.CurrentPackageId);
        }

        Assert.Null(FrontedPluginRegistrationContext.CurrentPackageId);
    }

    [Fact]
    public void BeginScopeWithNull_AllowsNonPluginHostRegistration()
    {
        Assert.Null(FrontedPluginRegistrationContext.CurrentPackageId);

        using (FrontedPluginRegistrationContext.BeginScope(null))
        {
            Assert.Null(FrontedPluginRegistrationContext.CurrentPackageId);
        }

        Assert.Null(FrontedPluginRegistrationContext.CurrentPackageId);
    }
}
