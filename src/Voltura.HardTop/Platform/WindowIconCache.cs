using System.Windows.Media;

namespace Voltura.HardTop.Platform;

// Own managed snapshots only; native class icon handles belong to the other app.
internal sealed class WindowIconCache(Func<nint, ImageSource> load)
{
    private readonly Dictionary<WindowIdentity, (nint Handle, ImageSource Image)> images = [];

    internal ImageSource? Get(WindowIdentity identity, nint handle)
    {
        if (handle == 0)
        {
            images.Remove(identity);
            return null;
        }
        if (images.TryGetValue(identity, out var cached) && cached.Handle == handle)
            return cached.Image;
        images.Remove(identity);
        try
        {
            var image = load(handle);
            image.Freeze();
            images[identity] = (handle, image);
            return image;
        }
        catch (ArgumentException) { return null; }
    }

    internal void Retain(IReadOnlySet<WindowIdentity> identities)
    {
        foreach (var identity in images.Keys.Where(identity => !identities.Contains(identity)).ToArray())
            images.Remove(identity);
    }
}
