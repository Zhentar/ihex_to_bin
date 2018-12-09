using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ihexParser
{
	class Program
	{
		static void Main(string[] args)
		{
			var stream = File.OpenRead(@"C:\Users\zhent\OneDrive\Documents\ReverseEngineering\BINF0130.hex");
			var memoryStream = new MemoryStream();
			stream.CopyTo(memoryStream);
			ReadOnlySpan<byte> bytes = memoryStream.ToArray().AsSpan();
			uint currentOffset = 0;
			uint lastSegmentStart = 0;
			uint baseAddress = 0;
			var outputStream = File.Create(@"C:\Users\zhent\OneDrive\Documents\ReverseEngineering\BINF0130.bin");
			while (!bytes.IsEmpty && bytes[0] == ':')
			{
				bytes.Read<byte>();
				//+------------------+---------------+-------------+----------------+---------------+-------------------+
				//|      RECORD      |      LOAD     |             |                |      INFO     |                   | 
				//|       MARK       |     RECLEN    |    OFFSET   |      RECTYP    |       or      |       CHKSUM      | 
				//|       ': '       |               |             |                |      DATA     |                   |
				//+------------------+---------------+-------------+----------------+---------------+-------------------+ 
				//     1- byte            1- byte        2- bytes        1- byte          n- bytes           1- byte
				var recLen = bytes.Read<byte>();
				var offset = bytes.Read<ushort>();
				if (BitConverter.IsLittleEndian)
				{
					offset = BinaryPrimitives.ReverseEndianness(offset);
				}
				var recType = bytes.Read<byte>();
				var data = bytes.Split(recLen);
				var checksum = bytes.Read<byte>();
				switch (recType)
				{
					case 0:
						var position = currentOffset + offset - baseAddress;
						if (position != outputStream.Position)
						{
							if (outputStream.Position != lastSegmentStart)
							{
								Console.WriteLine($"Wrote segment from {lastSegmentStart + baseAddress:X8} to {outputStream.Position + baseAddress:X8}");
							}

							lastSegmentStart = position;
							outputStream.Seek(position, SeekOrigin.Begin);
						}
						outputStream.Write(data);
						break;
					case 1:
						if (outputStream.Position != lastSegmentStart)
						{
							Console.WriteLine($"Wrote segment from {lastSegmentStart + baseAddress:X8} to {outputStream.Position + baseAddress:X8}");
						}
						//end of file... at the end of the file.
						break;
					case 2:
						currentOffset = (uint)(BinaryPrimitives.ReadUInt16BigEndian(data) << 4);
						break;
					case 4:
						currentOffset = (uint)(BinaryPrimitives.ReadUInt16BigEndian(data) << 16);
						if (outputStream.Position == 0)
						{
							baseAddress = currentOffset;
							Console.WriteLine($"Base Address: {baseAddress:X8}");
						}
						break;
					case 5:
						Console.WriteLine($"Entry point: {BinaryPrimitives.ReadUInt32BigEndian(data):X8}");
						break;
					default:
						Console.WriteLine($"Unknown Record type {recType}");
						break;
				}
			}

			Console.WriteLine($"Completed! With {bytes.Length} remaining bytes in the file unparsed.");
			Console.WriteLine($"{outputStream.Length:N0} binary output length");
			outputStream.Close();
			Console.ReadLine();
		}
	}

	static class SpanUtils
	{
		public static T Read<T>(ref this ReadOnlySpan<byte> @this)
		{
			var result = Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(@this));
			@this = @this.Slice(Unsafe.SizeOf<T>());
			return result;
		}

		public static ReadOnlySpan<byte> Split(ref this ReadOnlySpan<byte> @this, int index)
		{
			var result = @this.Slice(0, index);
			@this = @this.Slice(index);
			return result;
		}
	}
}
