using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BetterCalibration.Core;


public static class BinaryIO {
    public static byte ReadByte(Stream stream) {
        int value = stream.ReadByte();
        if(value == -1) throw new EndOfStreamException();
        return (byte) value;
    }

    public static byte[] ReadBytes(Stream stream, int count) {
        byte[] buffer = new byte[count];
        int current = 0;
        while(current < count) {
            int read = stream.Read(buffer, current, count - current);
            if(read == 0) throw new EndOfStreamException();
            current += read;
        }
        return buffer;
    }

    public static void WriteInt(Stream stream, int value) {
        stream.WriteByte((byte) (value >> 24));
        stream.WriteByte((byte) (value >> 16));
        stream.WriteByte((byte) (value >> 8));
        stream.WriteByte((byte) value);
    }

    public static int ReadInt(Stream stream) =>
        (ReadByte(stream) << 24) + (ReadByte(stream) << 16) + (ReadByte(stream) << 8) + ReadByte(stream);


    public static void WriteFloat(Stream stream, float value) {
        byte[] data = BitConverter.GetBytes(value);
        stream.WriteByte(data[3]);
        stream.WriteByte(data[2]);
        stream.WriteByte(data[1]);
        stream.WriteByte(data[0]);
    }

    public static float ReadFloat(Stream stream) {
        byte[] data = new byte[4];
        data[3] = ReadByte(stream);
        data[2] = ReadByte(stream);
        data[1] = ReadByte(stream);
        data[0] = ReadByte(stream);
        return BitConverter.ToSingle(data, 0);
    }

    public static void WriteBool(Stream stream, bool value) => stream.WriteByte(value ? (byte) 1 : (byte) 0);

    public static bool ReadBool(Stream stream) => ReadByte(stream) != 0;

    public static void WriteString(Stream stream, string value) {
        byte[] data = Encoding.UTF8.GetBytes(value);
        WriteInt(stream, data.Length);
        stream.Write(data, 0, data.Length);
    }

    public static string ReadString(Stream stream) {
        int length = ReadInt(stream);
        return length == -1 ? null : Encoding.UTF8.GetString(ReadBytes(stream, length));
    }


    public static void WriteFloatList(Stream stream, IList<float> values) {
        if(values == null) {
            WriteInt(stream, -1);
            return;
        }
        WriteInt(stream, values.Count);
        for(int i = 0; i < values.Count; i++) WriteFloat(stream, values[i]);
    }

    public static List<float> ReadFloatList(Stream stream) {
        int count = ReadInt(stream);
        if(count == -1) return null;
        List<float> values = new(count);
        for(int i = 0; i < count; i++) values.Add(ReadFloat(stream));
        return values;
    }
}
