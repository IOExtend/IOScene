using System.Text.Json;
using System.Text.Json.Serialization;

namespace IOScene;

// Math values are written as plain number arrays, and vector lists as one flat array. The converters are attached
// with attributes, so any System.Text.Json serialisation of these types gives the same encoding as SceneJson.

/// <summary>How a math value maps to a fixed count of numbers.</summary>
internal interface INumbers<T>
{
    static abstract int Count { get; }
    static abstract T Join(ReadOnlySpan<double> n);
    static abstract void Split(T value, Span<double> n);
}

internal readonly struct Vector2dNumbers : INumbers<Vector2d>
{
    public static int Count => 2;
    public static Vector2d Join(ReadOnlySpan<double> n) => new(n[0], n[1]);
    public static void Split(Vector2d value, Span<double> n) { n[0] = value.X; n[1] = value.Y; }
}

internal readonly struct Vector3dNumbers : INumbers<Vector3d>
{
    public static int Count => 3;
    public static Vector3d Join(ReadOnlySpan<double> n) => new(n[0], n[1], n[2]);
    public static void Split(Vector3d value, Span<double> n) { n[0] = value.X; n[1] = value.Y; n[2] = value.Z; }
}

internal readonly struct Transform3dNumbers : INumbers<Transform3d>
{
    public static int Count => 12;

    public static Transform3d Join(ReadOnlySpan<double> n) =>
        new(new(n[0], n[1], n[2]), new(n[3], n[4], n[5]), new(n[6], n[7], n[8]), new(n[9], n[10], n[11]));

    public static void Split(Transform3d value, Span<double> n)
    {
        Vector3dNumbers.Split(value.XAxis, n);
        Vector3dNumbers.Split(value.YAxis, n[3..]);
        Vector3dNumbers.Split(value.ZAxis, n[6..]);
        Vector3dNumbers.Split(value.Translation, n[9..]);
    }
}

internal readonly struct RgbaColorNumbers : INumbers<RgbaColor>
{
    public static int Count => 4;
    public static RgbaColor Join(ReadOnlySpan<double> n) => new(Channel(n[0]), Channel(n[1]), Channel(n[2]), Channel(n[3]));
    public static void Split(RgbaColor value, Span<double> n) { n[0] = value.R; n[1] = value.G; n[2] = value.B; n[3] = value.A; }

    private static byte Channel(double value) => value is >= 0 and <= 255 && value == Math.Floor(value)
        ? (byte)value
        : throw new JsonException("A colour channel is an integer from 0 to 255.");
}

/// <summary>One math value as an array of exactly <c>Count</c> numbers.</summary>
internal sealed class NumberArrayConverter<T, TNumbers> : JsonConverter<T> where TNumbers : INumbers<T>
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Span<double> numbers = stackalloc double[TNumbers.Count];
        if (reader.TokenType != JsonTokenType.StartArray) throw Wrong();
        for (int i = 0; i < numbers.Length; i++)
        {
            if (!reader.Read() || reader.TokenType != JsonTokenType.Number) throw Wrong();
            numbers[i] = reader.GetDouble();
        }
        if (!reader.Read() || reader.TokenType != JsonTokenType.EndArray) throw Wrong();
        return TNumbers.Join(numbers);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        Span<double> numbers = stackalloc double[TNumbers.Count];
        TNumbers.Split(value, numbers);
        writer.WriteStartArray();
        foreach (double number in numbers) writer.WriteNumberValue(number);
        writer.WriteEndArray();
    }

    private static JsonException Wrong() => new($"{typeof(T).Name} is an array of {TNumbers.Count} numbers.");
}

/// <summary>A list of math values as one flat array of numbers.</summary>
internal sealed class FlatListConverter<T, TNumbers> : JsonConverter<IReadOnlyList<T>> where TNumbers : INumbers<T>
{
    public override IReadOnlyList<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException($"A list of {typeof(T).Name} is one flat array of numbers.");
        var list = new List<T>();
        Span<double> numbers = stackalloc double[TNumbers.Count];
        int filled = 0;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.Number) throw new JsonException($"A list of {typeof(T).Name} is one flat array of numbers.");
            numbers[filled++] = reader.GetDouble();
            if (filled == numbers.Length)
            {
                list.Add(TNumbers.Join(numbers));
                filled = 0;
            }
        }
        if (filled != 0) throw new JsonException($"A list of {typeof(T).Name} has a multiple of {TNumbers.Count} numbers.");
        return list;
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<T> value, JsonSerializerOptions options)
    {
        Span<double> numbers = stackalloc double[TNumbers.Count];
        writer.WriteStartArray();
        foreach (var item in value)
        {
            TNumbers.Split(item, numbers);
            foreach (double number in numbers) writer.WriteNumberValue(number);
        }
        writer.WriteEndArray();
    }
}
