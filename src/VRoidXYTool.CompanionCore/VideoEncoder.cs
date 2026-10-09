using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace VRoidXYTool.CompanionCore;

// Only the writer task touches the process pipe. Unity's thread never waits on an encoder.
public sealed class VideoEncoder : IDisposable
{
    private readonly BlockingCollection<(byte[] Pixels,int Frame)> queue = new(2);
    private readonly Process process;
    private readonly Task writer;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly StringBuilder errors = new();
    private readonly int bytesPerFrame, fps;
    private readonly string temporary, destination;
    private Task<string>? completion;
    private int stopped, dropped;
    public int DroppedFrames => Volatile.Read(ref dropped);
    public VideoEncoder(string executable,string output,int width,int height,int fps,int bitrate)
    {
        if(!File.Exists(executable))throw new FileNotFoundException("Configure an existing FFmpeg executable for recording.",executable);
        if(width<2 || height<2 || width>4096 || height>4096 || width%2!=0 || height%2!=0 || fps<1 || fps>240 || bitrate<256000 || bitrate>100000000)throw new InvalidDataException("Invalid recording resolution, FPS or bitrate.");
        this.fps=fps;bytesPerFrame=checked(width*height*4);destination=Path.GetFullPath(output);
        if(!Path.GetExtension(destination).Equals(".mp4",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Recording output must be MP4.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if(File.Exists(destination))throw new IOException("Recording output already exists.");
        temporary=destination+".partial.mp4";
        var start=new ProcessStartInfo(Path.GetFullPath(executable)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardError=true};
        string[] arguments={"-hide_banner","-loglevel","error","-nostdin","-n","-f","rawvideo","-pixel_format","rgba","-video_size",$"{width}x{height}","-framerate",fps.ToString(CultureInfo.InvariantCulture),"-i","pipe:0","-an","-c:v","libx264","-preset","veryfast","-b:v",bitrate.ToString(CultureInfo.InvariantCulture),"-pix_fmt","yuv420p","-movflags","+faststart",temporary};
        foreach(string argument in arguments)start.ArgumentList.Add(argument);
        process=new Process{StartInfo=start};
        process.ErrorDataReceived+=(_,e)=>{if(e.Data!=null)lock(errors){if(errors.Length<4096)errors.AppendLine(e.Data);}};
        try {if(!process.Start())throw new IOException("Encoder could not start.");process.BeginErrorReadLine();}
        catch {process.Dispose();queue.Dispose();throw;}
        writer=Task.Run(WriteFrames);
    }
    public bool Submit(byte[] pixels)
    {
        if(pixels.Length!=bytesPerFrame)throw new InvalidDataException("Recording dimensions changed.");
        if(Volatile.Read(ref stopped)!=0)return false;
        if(writer.IsFaulted)throw new IOException("Encoder stopped accepting frames.",writer.Exception);
        int frame=checked((int)(clock.Elapsed.TotalSeconds*fps));
        if(queue.TryAdd((pixels,frame)))return true;
        Interlocked.Increment(ref dropped);return false;
    }
    private void WriteFrames()
    {
        byte[]? previous=null;int next=0;
        try
        {
            foreach(var packet in queue.GetConsumingEnumerable())
            {
                if(packet.Frame<next)continue;
                // Repeat the last frame over missed captures to preserve wall-clock duration.
                while(previous!=null && next<packet.Frame){process.StandardInput.BaseStream.Write(previous);next++;}
                process.StandardInput.BaseStream.Write(packet.Pixels);next++;previous=packet.Pixels;
            }
            int end=checked((int)(clock.Elapsed.TotalSeconds*fps));
            while(previous!=null && next<end){process.StandardInput.BaseStream.Write(previous);next++;}
        }
        finally {process.StandardInput.Close();}
    }
    public Task<string> StopAsync()
    {
        if(completion!=null)return completion;
        Interlocked.Exchange(ref stopped,1);clock.Stop();queue.CompleteAdding();
        return completion=Finish();
    }
    private async Task<string> Finish()
    {
        try
        {
            await writer.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
            if(process.ExitCode!=0 || !File.Exists(temporary) || new FileInfo(temporary).Length==0)
                throw new IOException("Recording could not be finalized: "+errors);
            File.Move(temporary,destination);return destination;
        }
        catch
        {
            if(!process.HasExited)process.Kill();
            throw; // Preserve partial data for recovery instead of presenting it as a finished MP4.
        }
        finally {process.Dispose();}
    }
    public void Dispose() {try {StopAsync().GetAwaiter().GetResult();} finally {queue.Dispose();}}
}
