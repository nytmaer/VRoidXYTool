using System.Diagnostics;
using System.Text.Json;
using VRoidXYTool.CompanionCore;

if(args.Length!=1)throw new ArgumentException("Pass the FFmpeg executable path for the recording integration test.");
string ffmpeg=Path.GetFullPath(args[0]);
string ffprobe=Path.Combine(Path.GetDirectoryName(ffmpeg)!,"ffprobe.exe");
string root=Path.Combine(Path.GetTempPath(),"vroid-recording-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    string output=Path.Combine(root,"two scenes.mp4");
    Reject(()=>new VideoEncoder(ffmpeg,output,63,64,20,1000000),"odd capture dimensions rejected before starting encoder");
    Reject(()=>new VideoEncoder(ffmpeg,output,64,64,0,1000000),"invalid frame rate rejected");
    using(var encoder=new VideoEncoder(ffmpeg,output,64,64,20,1000000))
    {
        for(int frame=0;frame<12;frame++)
        {
            byte[] pixels=new byte[64*64*4];
            for(int pixel=0;pixel<pixels.Length;pixel+=4){pixels[pixel]=frame<6?(byte)255:(byte)0;pixels[pixel+2]=frame>=6?(byte)255:(byte)0;pixels[pixel+3]=255;}
            encoder.Submit(pixels); // Startup backpressure may skip captures; the queue stays bounded.
            await Task.Delay(50);
        }
        byte[] blue=new byte[64*64*4];for(int i=0;i<blue.Length;i+=4){blue[i+2]=255;blue[i+3]=255;}
        bool accepted=false;for(int attempt=0;attempt<40 && !accepted;attempt++){accepted=encoder.Submit(blue);if(!accepted)await Task.Delay(100);}
        Check(accepted,"encoder accepts final scene after startup backpressure");
        string final=await encoder.StopAsync();
        Check(final==output && File.Exists(output) && !File.Exists(output+".partial.mp4"),"successful stop publishes only a finalized MP4");
        Check(!encoder.Submit(new byte[64*64*4]),"late frames cannot enter a stopped recording");
        Check(await encoder.StopAsync()==output,"repeated stop is idempotent");
    }
    var info=new ProcessStartInfo(ffprobe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
    foreach(string argument in new[]{"-v","error","-show_streams","-show_format","-of","json",output})info.ArgumentList.Add(argument);
    using(var process=Process.Start(info)!)
    {
        string json=await process.StandardOutput.ReadToEndAsync();await process.WaitForExitAsync();Check(process.ExitCode==0,"ffprobe recognizes recorded MP4");
        using var data=JsonDocument.Parse(json);var streams=data.RootElement.GetProperty("streams");
        Check(streams.GetArrayLength()==1 && streams[0].GetProperty("codec_name").GetString()=="h264","recording contains one H.264 video stream");
        Check(streams[0].GetProperty("width").GetInt32()==64 && streams[0].GetProperty("height").GetInt32()==64,"recorded dimensions match capture dimensions");
        double duration=double.Parse(data.RootElement.GetProperty("format").GetProperty("duration").GetString()!,System.Globalization.CultureInfo.InvariantCulture);
        Check(duration>=.5 && duration<5,"recording duration follows wall-clock captures");
    }
    var decode=new ProcessStartInfo(ffmpeg){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
    foreach(string argument in new[]{"-v","error","-i",output,"-f","rawvideo","-pix_fmt","rgba","pipe:1"})decode.ArgumentList.Add(argument);
    using(var process=Process.Start(decode)!)
    {
        using var bytes=new MemoryStream();await process.StandardOutput.BaseStream.CopyToAsync(bytes);await process.WaitForExitAsync();Check(process.ExitCode==0,"MP4 decodes through the last frame");
        byte[] pixels=bytes.ToArray();int last=pixels.Length-64*64*4;
        Check(pixels.Length>=12*64*64*4 && pixels[0]>200 && pixels[2]<30 && pixels[last]<30 && pixels[last+2]>200,"decoded frames preserve both recorded scenes");
    }
    File.WriteAllText(output,"keep this existing recording");
    Reject(()=>new VideoEncoder(ffmpeg,output,64,64,20,1000000),"existing recordings cannot be overwritten");
    Check(File.ReadAllText(output)=="keep this existing recording","overwrite rejection preserves original file");
    string failedOutput=Path.Combine(root,"failed.mp4");
    var failedEncoder=new VideoEncoder(ffprobe,failedOutput,64,64,20,1000000);
    try
    {
        failedEncoder.Submit(new byte[64*64*4]);
        bool failed=false;try{await failedEncoder.StopAsync();}catch(Exception e)when(e is IOException or InvalidOperationException or AggregateException){failed=true;}
        Check(failed && !File.Exists(failedOutput),"encoder failure cannot publish a completed recording");
    }
    finally{try{failedEncoder.Dispose();}catch(Exception e)when(e is IOException or InvalidOperationException or AggregateException){}}
    Console.WriteLine("PASS: recording encoder integration");
}
finally{Directory.Delete(root,true);}
static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
static void Reject(Action action,string message){bool rejected=false;try{action();}catch(Exception e)when(e is IOException or InvalidDataException){rejected=true;}Check(rejected,message);}
