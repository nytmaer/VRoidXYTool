using System.Buffers.Binary;
using System.Text;

namespace VRoidXYTool.CompanionCore;

public static class VmdGuard
{
    public static void Validate(ReadOnlySpan<byte> data)
    {
        if (data.Length < 58 || data.Length > 64 * 1024 * 1024 || !data[..30].StartsWith(Encoding.ASCII.GetBytes("Vocaloid Motion Data 0002"))) throw new InvalidDataException("Expected a bounded VMD 0002 motion file.");
        int offset=50, total=0;
        Section(data,ref offset,ref total,111,true);
        Section(data,ref offset,ref total,23,true);
        Section(data,ref offset,ref total,61,false);
        Section(data,ref offset,ref total,28,false);
        if(offset==data.Length)return;
        Section(data,ref offset,ref total,9,false);
        if(offset==data.Length)return;
        int frames=Count(data,ref offset,9);
        for(int i=0;i<frames;i++)
        {
            Require(data,offset,9);
            if(BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset,4))>1000000) throw new InvalidDataException("VMD frame number exceeds limit.");
            offset+=5; int ik=Count(data,ref offset,21); offset=checked(offset+ik*21);
        }
        if(offset!=data.Length)throw new InvalidDataException("Unexpected VMD trailing data.");
    }
    private static void Require(ReadOnlySpan<byte> data,int offset,int length)
    {
        if(offset<0 || length<0 || offset>data.Length-length)throw new InvalidDataException("Truncated VMD section.");
    }
    private static int Count(ReadOnlySpan<byte> data,ref int offset,int recordSize)
    {
        Require(data,offset,4); uint count=BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset,4));offset+=4;
        if(count>500000 || count>(uint)((data.Length-offset)/recordSize))throw new InvalidDataException("Invalid VMD section count.");
        return (int)count;
    }
    private static void Section(ReadOnlySpan<byte> data,ref int offset,ref int total,int size,bool motion)
    {
        int count=Count(data,ref offset,size); total=checked(total+count);
        if(total>500000)throw new InvalidDataException("Too many VMD keyframes.");
        for(int i=0;i<count;i++)
        {
            if(motion)
            {
                int frameOffset=offset+15;
                if(BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(frameOffset,4))>1000000)throw new InvalidDataException("VMD frame number exceeds limit.");
                int floats=size==111?7:1;
                for(int f=0;f<floats;f++)
                    if(!float.IsFinite(BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data.Slice(frameOffset+4+f*4,4)))))throw new InvalidDataException("Non-finite VMD motion value.");
            }
            offset+=size;
        }
    }
}
