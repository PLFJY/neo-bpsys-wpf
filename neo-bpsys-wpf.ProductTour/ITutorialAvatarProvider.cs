using System.Windows.Media;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// Describes the pose requested for a tutorial guide avatar.
/// </summary>
public enum TutorialAvatarPose
{
    /// <summary>Neutral idle pose.</summary>
    Idle,

    /// <summary>Pose pointing toward the upper-left direction.</summary>
    LeftTop,

    /// <summary>Pose pointing toward the lower-left direction.</summary>
    LeftBottom,

    /// <summary>Pose pointing toward the upper-right direction.</summary>
    RightTop,

    /// <summary>Pose pointing toward the lower-right direction.</summary>
    RightBottom
}

/// <summary>
/// Represents a tutorial guide avatar image and localized display name.
/// </summary>
public sealed class TutorialAvatar
{
    /// <summary>Gets the localized avatar display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Gets the avatar image source.</summary>
    public required ImageSource ImageSource { get; init; }
}

/// <summary>
/// Provides tutorial guide avatars for overlay UI.
/// </summary>
public interface ITutorialAvatarProvider
{
    /// <summary>
    /// Gets an avatar for the requested pose.
    /// </summary>
    /// <param name="pose">Requested avatar pose.</param>
    /// <returns>The avatar, or <see langword="null" /> when no avatar is available.</returns>
    TutorialAvatar? GetAvatar(TutorialAvatarPose pose);
}

/// <summary>
/// Empty tutorial avatar provider used when the host application does not provide guide assets.
/// </summary>
public sealed class NoOpTutorialAvatarProvider : ITutorialAvatarProvider
{
    /// <inheritdoc />
    public TutorialAvatar? GetAvatar(TutorialAvatarPose pose) => null;
}
