using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace TVFollow
{
    // A picture of the TV's screen, saved under a name that says what's in it. The TV sends a
    // JPEG or a PNG as it pleases, and a JPEG in a file called shot.png opens in some programs
    // and not in others.
    static class DevPicture
    {
        // "png" or "jpg": what the picture's first bytes say it is, or failing that the TV's word
        public static string Kind(byte[] picture, string said)
        {
            if (picture.Length >= 4 && picture[0] == 0x89 && picture[1] == 'P' && picture[2] == 'N' && picture[3] == 'G') return "png";
            if (picture.Length >= 3 && picture[0] == 0xFF && picture[1] == 0xD8 && picture[2] == 0xFF) return "jpg";
            return said;
        }

        // Under a name that ends in .png or .jpg (or .jpeg) the picture is saved as that kind,
        // made over when the TV sent the other. Any other name gets the picture's own kind
        // added to it. Returns the name it was saved under.
        public static string Save(string path, byte[] picture, string said)
        {
            string kind = Kind(picture, said);
            string named = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            if (named == "jpeg") named = "jpg";
            if (named != "png" && named != "jpg")
            {
                path += "." + kind;
                named = kind;
            }
            File.WriteAllBytes(path, named == kind ? picture : As(picture, named));
            return path;
        }

        static byte[] As(byte[] picture, string kind)
        {
            try
            {
                using (var from = new MemoryStream(picture))
                using (Image image = Image.FromStream(from))
                using (var to = new MemoryStream())
                {
                    if (kind == "png") image.Save(to, ImageFormat.Png);
                    else
                    {
                        // Windows' own setting for JPEGs is a rough one, so a finer one is asked for
                        ImageCodecInfo jpeg = Array.Find(ImageCodecInfo.GetImageEncoders(), c => c.FormatID == ImageFormat.Jpeg.Guid);
                        using (var how = new EncoderParameters(1))
                        {
                            how.Param[0] = new EncoderParameter(Encoder.Quality, 92L);
                            image.Save(to, jpeg, how);
                        }
                    }
                    return to.ToArray();
                }
            }
            catch (Exception e)
            {
                throw new InvalidDataException("what the TV sent couldn't be made into a ." + kind + " picture (" + e.Message + ")");
            }
        }
    }
}
