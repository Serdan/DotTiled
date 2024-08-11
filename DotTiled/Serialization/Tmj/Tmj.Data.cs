using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

namespace DotTiled;

internal partial class Tmj
{
  internal static Data ReadDataAsChunks(JsonElement element, DataCompression? compression, DataEncoding encoding)
  {
    var chunks = element.GetValueAsList<Chunk>(e => ReadChunk(e, compression, encoding)).ToArray();
    return new Data
    {
      Chunks = chunks,
      Compression = compression,
      Encoding = encoding,
      FlippingFlags = null,
      GlobalTileIDs = null
    };
  }

  internal static Chunk ReadChunk(JsonElement element, DataCompression? compression, DataEncoding encoding)
  {
    var data = ReadDataWithoutChunks(element, compression, encoding);

    var x = element.GetRequiredProperty<int>("x");
    var y = element.GetRequiredProperty<int>("y");
    var width = element.GetRequiredProperty<uint>("width");
    var height = element.GetRequiredProperty<uint>("height");

    return new Chunk
    {
      X = x,
      Y = y,
      Width = width,
      Height = height,
      GlobalTileIDs = data.GlobalTileIDs!,
      FlippingFlags = data.FlippingFlags!
    };
  }

  internal static Data ReadDataWithoutChunks(JsonElement element, DataCompression? compression, DataEncoding encoding)
  {
    if (encoding == DataEncoding.Csv)
    {
      // Array of uint
      var data = element.GetValueAsList<uint>(e => e.GetValueAs<uint>()).ToArray();
      var (globalTileIDs, flippingFlags) = Helpers.Data.ReadAndClearFlippingFlagsFromGIDs(data);
      return new Data { Encoding = encoding, Compression = compression, GlobalTileIDs = globalTileIDs, FlippingFlags = flippingFlags, Chunks = null };
    }
    else if (encoding == DataEncoding.Base64)
    {
      var base64Data = element.GetBytesFromBase64();

      if (compression == null)
      {
        var data = Helpers.Data.ReadBytesAsInt32Array(base64Data);
        var (globalTileIDs, flippingFlags) = Helpers.Data.ReadAndClearFlippingFlagsFromGIDs(data);
        return new Data { Encoding = encoding, Compression = compression, GlobalTileIDs = globalTileIDs, FlippingFlags = flippingFlags, Chunks = null };
      }

      using var stream = new MemoryStream(base64Data);
      var decompressed = compression switch
      {
        DataCompression.GZip => Helpers.Data.DecompressGZip(stream),
        DataCompression.ZLib => Helpers.Data.DecompressZLib(stream),
        _ => throw new JsonException($"Unsupported compression '{compression}'.")
      };

      {
        var (globalTileIDs, flippingFlags) = Helpers.Data.ReadAndClearFlippingFlagsFromGIDs(decompressed);
        return new Data { Encoding = encoding, Compression = compression, GlobalTileIDs = globalTileIDs, FlippingFlags = flippingFlags, Chunks = null };
      }
    }

    throw new JsonException($"Unsupported encoding '{encoding}'.");
  }
}
