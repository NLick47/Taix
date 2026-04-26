use windows::Win32::Media::Audio::{eConsole, eRender, IMMDeviceEnumerator, MMDeviceEnumerator};
use windows::Win32::System::Com::{
    CoCreateInstance, CoInitializeEx, CoUninitialize, CLSCTX_ALL, COINIT_APARTMENTTHREADED,
};
use windows::Win32::Media::Audio::Endpoints::IAudioMeterInformation;

pub fn is_windows_playing_sound() -> bool {
    unsafe {
        let hr = CoInitializeEx(None, COINIT_APARTMENTTHREADED);
        let needs_uninit = hr == windows::core::HRESULT(0); // s_ok 才需要反初始化
        if hr.is_err() && hr.0 as u32 != 0x80010106 {
            // 0x80010106 = RPC_E_CHANGED_MODE，不用管继续跑
            return false;
        }

        let enumerator = match CoCreateInstance::<_, IMMDeviceEnumerator>(
            &MMDeviceEnumerator,
            None,
            CLSCTX_ALL,
        ) {
            Ok(e) => e,
            Err(_) => {
                if needs_uninit {
                    CoUninitialize();
                }
                return false;
            }
        };

        let device = match enumerator.GetDefaultAudioEndpoint(eRender, eConsole) {
            Ok(d) => d,
            Err(_) => {
                if needs_uninit {
                    CoUninitialize();
                }
                return false;
            }
        };

        let meter: windows::core::Result<IAudioMeterInformation> = device.Activate(CLSCTX_ALL, None);
        let meter = match meter {
            Ok(m) => m,
            Err(_) => {
                if needs_uninit {
                    CoUninitialize();
                }
                return false;
            }
        };

        let playing = match meter.GetPeakValue() {
            Ok(peak) => peak > 0.0001,
            Err(_) => false,
        };

        if needs_uninit {
            CoUninitialize();
        }
        playing
    }
}
