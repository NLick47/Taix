import CoreAudio
import Foundation

struct AudioMonitor {
    func isAudioPlaying() -> Bool {
        checkAudioPlaying()
    }

    private func checkAudioPlaying() -> Bool {
        let systemObject = AudioObjectID(kAudioObjectSystemObject)

        var propertyAddress = AudioObjectPropertyAddress(
            mSelector: kAudioHardwarePropertyDefaultOutputDevice,
            mScope: kAudioObjectPropertyScopeGlobal,
            mElement: kAudioObjectPropertyElementMain
        )

        var deviceID: AudioObjectID = kAudioObjectUnknown
        var size = UInt32(MemoryLayout<AudioObjectID>.size)

        let deviceResult = AudioObjectGetPropertyData(
            systemObject,
            &propertyAddress,
            0,
            nil,
            &size,
            &deviceID
        )

        guard deviceResult == noErr, deviceID != kAudioObjectUnknown else {
            return false
        }

        propertyAddress.mSelector = kAudioDevicePropertyDeviceIsRunningSomewhere
        propertyAddress.mScope = kAudioObjectPropertyScopeGlobal

        var isRunning: UInt32 = 0
        size = UInt32(MemoryLayout<UInt32>.size)

        let runningResult = AudioObjectGetPropertyData(
            deviceID,
            &propertyAddress,
            0,
            nil,
            &size,
            &isRunning
        )

        return runningResult == noErr && isRunning != 0
    }
}
