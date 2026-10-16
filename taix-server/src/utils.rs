use chrono::{DateTime, NaiveDate, NaiveDateTime, TimeZone, Utc};
use chrono_tz::Tz;
use std::sync::OnceLock;

/// 解析 IANA 时区 ID。解析失败时回退到 UTC。
pub fn parse_timezone(tz_id: &str) -> Tz {
    match tz_id.parse::<Tz>() {
        Ok(tz) => tz,
        Err(_) => {
            tracing::warn!("未知时区 ID: {}，回退到 UTC", tz_id);
            chrono_tz::UTC
        }
    }
}


pub fn host_timezone() -> Tz {
    if let Ok(id) = std::env::var("TAIX_TZ") {
        match id.trim().parse::<Tz>() {
            Ok(tz) => return tz,
            Err(_) => tracing::warn!("TAIX_TZ 无效: {}，改用系统时区", id),
        }
    }
    static SYSTEM: OnceLock<Tz> = OnceLock::new();
    *SYSTEM.get_or_init(|| {
        iana_time_zone::get_timezone()
            .ok()
            .and_then(|id| id.parse::<Tz>().ok())
            .unwrap_or_else(|| {
                tracing::warn!("无法解析宿主机时区，回退 Asia/Shanghai");
                chrono_tz::Asia::Shanghai
            })
    })
}

fn resolve_local(tz: &Tz, naive: NaiveDateTime) -> chrono::DateTime<Tz> {
    match tz.from_local_datetime(&naive) {
        chrono::LocalResult::Single(dt) => dt,
        chrono::LocalResult::Ambiguous(earliest, _) => earliest,
        chrono::LocalResult::None => {
            let mut probe = naive;
            for _ in 0..(4 * 60) {
                probe += chrono::Duration::minutes(1);
                match tz.from_local_datetime(&probe) {
                    chrono::LocalResult::Single(dt) => return dt,
                    chrono::LocalResult::Ambiguous(earliest, _) => return earliest,
                    chrono::LocalResult::None => {}
                }
            }
            tz.from_utc_datetime(&naive)
        }
    }
}

/// 将指定时区的本地日期转换为 UTC 的 [00:00, 次日 00:00) 范围
pub fn tz_date_to_utc_range(date: NaiveDate, tz: &Tz) -> (DateTime<Utc>, DateTime<Utc>) {
    let naive_start = date.and_hms_opt(0, 0, 0).unwrap();
    let naive_end = date.succ_opt().unwrap().and_hms_opt(0, 0, 0).unwrap();
    (
        resolve_local(tz, naive_start).with_timezone(&Utc),
        resolve_local(tz, naive_end).with_timezone(&Utc),
    )
}

/// 将指定时区的本地 NaiveDateTime 转换为 UTC
pub fn tz_naive_to_utc(naive: NaiveDateTime, tz: &Tz) -> DateTime<Utc> {
    match tz.from_local_datetime(&naive) {
        chrono::LocalResult::Single(dt) => dt.with_timezone(&Utc),
        chrono::LocalResult::Ambiguous(earliest, _) => earliest.with_timezone(&Utc),
        chrono::LocalResult::None => {
            // DST gap: 本地时间不存在，将 naive 当作 UTC 直接转换（近似处理）
            tz.from_utc_datetime(&naive).with_timezone(&Utc)
        }
    }
}

/// 将本地日期范围转换为 DailyLog 查询所需的 UTC 日期范围 [utc_start, utc_end)。
/// 由于一个本地日期可能跨越两个 UTC 日期，为了查询不遗漏，
/// utc_start 取本地 start 00:00 对应的 UTC 日期，
/// utc_end 取本地 end+1 00:00 对应的 UTC 日期（半开区间）。
pub fn tz_date_range_to_utc_date_range(
    start: NaiveDate,
    end: NaiveDate,
    tz: &Tz,
) -> (NaiveDate, NaiveDate) {
    let utc_start = tz_date_to_utc_range(start, tz).0.date_naive();
    let utc_end = tz_date_to_utc_range(end, tz).1.date_naive();
    (utc_start, utc_end)
}

pub fn last_day_of_month(year: i32, month: u32) -> u32 {
    match month {
        1 | 3 | 5 | 7 | 8 | 10 | 12 => 31,
        4 | 6 | 9 | 11 => 30,
        2 => {
            if (year % 4 == 0 && year % 100 != 0) || (year % 400 == 0) {
                29
            } else {
                28
            }
        }
        _ => 30,
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use chrono::SecondsFormat;

    #[test]
    fn host_timezone_honors_override() {
        // 覆盖值每次调用生效（脚本/多实例依赖此语义）
        std::env::set_var("TAIX_TZ", "Pacific/Kiritimati");
        assert_eq!(host_timezone().name(), "Pacific/Kiritimati");
        std::env::remove_var("TAIX_TZ");
        // 回退到系统时区（解析失败时也会回退 Asia/Shanghai，必定可用）
        assert!(!host_timezone().name().is_empty());
    }

    #[test]
    fn dst_gap_day_starts_at_first_valid_local_time() {
        // 智利 2026-09-06：本地 00:00 不存在（直接跳到 01:00），当日应从 01:00(-03) 开始。
        // 修复前 None 分支按 UTC 解释 00:00，日界整整早 4 小时（把前一天算进今天）
        let tz: Tz = "America/Santiago".parse().unwrap();
        let day = NaiveDate::from_ymd_opt(2026, 9, 6).unwrap();
        let (start, end) = tz_date_to_utc_range(day, &tz);
        assert_eq!(
            start.to_rfc3339_opts(SecondsFormat::Secs, true),
            "2026-09-06T04:00:00Z",
            "智利 DST 空洞日应从第一个存在的本地时刻 01:00 开始"
        );
        assert_eq!(
            end.to_rfc3339_opts(SecondsFormat::Secs, true),
            "2026-09-07T03:00:00Z"
        );
        assert!(start < end);

        // 古巴 2026-03-08：同样 00:00 → 01:00(-04)
        let tz: Tz = "America/Havana".parse().unwrap();
        let day = NaiveDate::from_ymd_opt(2026, 3, 8).unwrap();
        let (start, end) = tz_date_to_utc_range(day, &tz);
        assert_eq!(
            start.to_rfc3339_opts(SecondsFormat::Secs, true),
            "2026-03-08T05:00:00Z"
        );
        assert_eq!(
            end.to_rfc3339_opts(SecondsFormat::Secs, true),
            "2026-03-09T04:00:00Z"
        );
    }

    #[test]
    fn normal_day_boundaries_keep_utc_offset() {
        let tz: Tz = "Asia/Shanghai".parse().unwrap();
        let day = NaiveDate::from_ymd_opt(2026, 10, 17).unwrap();
        let (start, end) = tz_date_to_utc_range(day, &tz);
        assert_eq!(
            start.to_rfc3339_opts(SecondsFormat::Secs, true),
            "2026-10-16T16:00:00Z"
        );
        assert_eq!(
            end.to_rfc3339_opts(SecondsFormat::Secs, true),
            "2026-10-17T16:00:00Z"
        );
        assert_eq!((end - start).num_hours(), 24, "无 DST 的普通日应是 24 小时");
    }
}
