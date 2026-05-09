//! 将子进程绑定到 Job Object，当持有 Job 句柄的进程退出时，
//! 操作系统自动终止 Job 内所有剩余进程，防止产生孤儿进程。

use windows::Win32::Foundation::{CloseHandle, HANDLE};
use windows::Win32::System::JobObjects::{
    AssignProcessToJobObject, CreateJobObjectW, JobObjectExtendedLimitInformation,
    QueryInformationJobObject, SetInformationJobObject, JOBOBJECT_EXTENDED_LIMIT_INFORMATION,
    JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE,
};
use windows::Win32::System::Threading::{OpenProcess, PROCESS_SET_QUOTA, PROCESS_TERMINATE};

pub struct JobObject {
    handle: HANDLE,
}

// SAFETY: Windows 句柄是内核对象，可在多线程间安全传递。
unsafe impl Send for JobObject {}

impl JobObject {
    pub fn new() -> anyhow::Result<Self> {
        unsafe {
            let handle = CreateJobObjectW(None, None)?;

            let mut info: JOBOBJECT_EXTENDED_LIMIT_INFORMATION = std::mem::zeroed();
            let mut returned = 0u32;
            QueryInformationJobObject(
                Some(handle),
                JobObjectExtendedLimitInformation,
                &mut info as *mut _ as *mut _,
                std::mem::size_of_val(&info) as u32,
                Some(&mut returned),
            )?;

            info.BasicLimitInformation.LimitFlags |= JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

            SetInformationJobObject(
                handle,
                JobObjectExtendedLimitInformation,
                &info as *const _ as *const _,
                std::mem::size_of_val(&info) as u32,
            )?;

            Ok(Self { handle })
        }
    }

    pub fn assign_process(&self, pid: u32) -> anyhow::Result<()> {
        // SAFETY: OpenProcess 参数为合法的 PID 和常量权限位；
        //         AssignProcessToJobObject 的句柄均为有效的系统句柄。
        unsafe {
            let process = OpenProcess(PROCESS_SET_QUOTA | PROCESS_TERMINATE, false, pid)?;
            let result = AssignProcessToJobObject(self.handle, process)
                .map_err(|e| anyhow::anyhow!("AssignProcessToJobObject failed: {e}"));
            let _ = CloseHandle(process);
            result
        }
    }
}

impl Drop for JobObject {
    fn drop(&mut self) {
        unsafe {
            let _ = CloseHandle(self.handle);
        }
    }
}
