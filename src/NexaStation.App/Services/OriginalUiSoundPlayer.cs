using System.Text;
using NexaStation.Customization.Services;
using NexaStation.Recovery;
using NexaStation.SystemIntegration.Services;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace NexaStation.App.Services;

/// <summary>Materializes and plays the project's original short PCM cues locally.</summary>
public sealed class OriginalUiSoundPlayer(ICurrentActivityService activity, INexaStationPaths paths) : IUiSoundPlayer,IDisposable
{
    private readonly Windows.Media.Playback.MediaPlayer _player=new();
    private readonly SemaphoreSlim _gate=new(1,1);

    public async Task PlayPackagedAsync(string assetKey,double volume,CancellationToken cancellationToken=default)
    {
        if(activity.Current.Duration is not null)return;
        if(Path.IsPathRooted(assetKey)||assetKey.Contains("..",StringComparison.Ordinal))throw new InvalidOperationException("UI sound asset keys must remain inside the managed sound library.");
        string pack=Path.GetFileName(Path.GetDirectoryName(assetKey))??"Default";string cue=Path.GetFileNameWithoutExtension(assetKey);string directory=Path.Combine(paths.UiSoundCache,pack);string path=Path.Combine(directory,$"{cue}.wav");
        await _gate.WaitAsync(cancellationToken);try{if(!File.Exists(path)){Directory.CreateDirectory(directory);await WriteOriginalCueAsync(path,pack,cue,cancellationToken);} _player.Volume=Math.Clamp(volume,0,1);_player.Source=MediaSource.CreateFromUri(new Uri(path));_player.Play();}finally{_gate.Release();}
    }

    private static async Task WriteOriginalCueAsync(string path,string pack,string cue,CancellationToken token)
    {
        const int sampleRate=22050;int milliseconds=cue switch{"Error"=>130,"Notification"=>110,"Startup"=>160,"Shutdown"=>150,"HoldComplete"=>120,_=>65};int sampleCount=sampleRate*milliseconds/1000;int seed=pack.Concat(cue).Aggregate(17,(value,ch)=>unchecked(value*31+ch));double baseFrequency=pack switch{"ArcadeLight"=>620,"MinimalClick"=>310,_=>440};baseFrequency+=Math.Abs(seed%180);byte[] pcm=new byte[sampleCount*2];for(int sample=0;sample<sampleCount;sample++){double time=sample/(double)sampleRate;double envelope=Math.Pow(1-sample/(double)sampleCount,2);double harmonic=cue=="Error"?.35*Math.Sin(2*Math.PI*(baseFrequency/2)*time):.15*Math.Sin(2*Math.PI*(baseFrequency*2)*time);short value=(short)(Math.Clamp((Math.Sin(2*Math.PI*baseFrequency*time)+harmonic)*envelope,-1,1)*short.MaxValue*.22);BitConverter.GetBytes(value).CopyTo(pcm,sample*2);}await using FileStream stream=new(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read,4096,true);using BinaryWriter writer=new(stream,Encoding.ASCII,true);writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+pcm.Length);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(sampleRate);writer.Write(sampleRate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(pcm.Length);writer.Write(pcm);await stream.FlushAsync(token);
    }

    public void Dispose(){_player.Dispose();_gate.Dispose();}
}
