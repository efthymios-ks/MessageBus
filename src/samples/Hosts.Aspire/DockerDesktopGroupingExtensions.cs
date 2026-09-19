namespace MessageBus.Hosts.Aspire;

internal static class DockerDesktopGroupingExtensions
{
    /// <summary>
    /// Tags the container with the compose-project label Docker Desktop groups by, so every
    /// container this AppHost owns collapses under one expandable app row rather than showing up
    /// separately. Aspire's DCP does not emit this label itself.
    /// </summary>
    public static IResourceBuilder<TContainer> WithDockerDesktopGroup<TContainer>(
        this IResourceBuilder<TContainer> builder,
        string groupName
    ) where TContainer : ContainerResource
        => builder.WithContainerRuntimeArgs("--label", $"com.docker.compose.project={groupName}");
}
