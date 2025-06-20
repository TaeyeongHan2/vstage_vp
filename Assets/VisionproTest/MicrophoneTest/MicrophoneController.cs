using UnityEngine;
using UnityEngine.UI;

/// <summary>An example script for showcasing a microphone usage in Unity engine
/// for more info please read the official documentation made by Unity: 
/// https://docs.unity3d.com/ScriptReference/Microphone.html
/// </summary>
public class MicrophoneController : MonoBehaviour
{
    #region Audio Clips
    //The Audio clips holding the recording in it's different states,
    //Created through the microphone class, no need to add them manualy 
    private AudioClip recordedClip; //The last recorded clip used for playing the recorded audio 
    private AudioClip currentlyRecordingClip; //The currently recorded clip
    private AudioClip _emptyClip; //Used for clearing the clip data, should be kept empty
    #endregion
   
    [Header("Recording length")]
    [Tooltip("Is the length of the AudioClip produced by the recording.")]
    [SerializeField] private int recordingLength;

    [Header("Buttons used for showcasing functionality")]
    [SerializeField] private Button playBt;
    [SerializeField] private Button stopBt;
    [SerializeField] private Button recordBt;

    /// <summary>Each device holds a list of available microphone devices, 
    /// identified by name, This will be the microphone we selected to use</summary>
    private string _microphone;

    //The Audio source from which the sound will be heard
    private AudioSource _audioSource;

    #region Set up
    private void Awake()
    {
        //Set the microphone that will be used for the reccording
        //(index 0 represents the defualt here)
        _microphone = Microphone.devices[0];
        SetAudioSources();
        SetupButton();
    }

    /// <summary>
    /// Set the audio sources for the recording: 
    /// Disabling loop & mute settings while setting the current recording to the target audio clip
    /// </summary>
    private void SetAudioSources()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.clip = currentlyRecordingClip;
        _audioSource.loop = false;
        _audioSource.mute = false;
    }

    private void SetupButton()
    {
        recordBt.onClick.AddListener(Record);
        playBt.onClick.AddListener(PlayRecording);
        stopBt.onClick.AddListener(StopRecording);
    }
    #endregion

    #region Micophone functionalities
    /// <summary>
    /// Records a new audio clip and caches it to the "currentlyRecordingClip" variable for further usage
    /// </summary>
    private void Record()
    {
        //If the audio source is corrently playing - stop it to allow a new recording to start
        if (_audioSource.isPlaying) _audioSource.Stop();

        //We end the last recording if it is mid recording
        StopRecording();

        //And set the currently played audio clip to be the new recording clip
        _audioSource.clip = currentlyRecordingClip;

        //Sets the currently recorded clip to start recording
        //using the device microphone, without looping, using a time limit while using the defualt frequency
        currentlyRecordingClip = Microphone.Start(_microphone, true, recordingLength, 44100);
    }

    /// <summary>
    /// Saves the newly recorded clip while ending the recording
    /// </summary>
    private void StopRecording()
    {
        recordedClip = currentlyRecordingClip;
        Microphone.End(_microphone);
    }

    /// <summary>
    /// Playes the latest recording by setting the recorded clip to the audio source and calling the Play method
    /// </summary>
    private void PlayRecording()
    {
        recordedClip = currentlyRecordingClip;
        _audioSource.clip = recordedClip;
        _audioSource.Play();
    }

    /// <summary>
    /// Stopes all audio from playing, 
    /// end the recording and clear the currently used clip by changing to an empty clip
    /// </summary>
    private void StopAllAudio()
    {
        _audioSource.Stop();
        Microphone.End(_microphone);
        _audioSource.clip = _emptyClip;
    }
    #endregion
}
