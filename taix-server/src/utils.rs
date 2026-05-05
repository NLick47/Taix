use chrono::{DateTime, NaiveDate, NaiveDateTime, TimeZone, Utc};
use chrono_tz::Tz;

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

/// 将指定时区的本地日期转换为 UTC 的 [00:00, 次日 00:00) 范围
pub fn tz_date_to_utc_range(date: NaiveDate, tz: &Tz) -> (DateTime<Utc>, DateTime<Utc>) {
    let local_start = tz.from_local_datetime(&date.and_hms_opt(0, 0, 0).unwrap()).unwrap();
    let local_end = tz.from_local_datetime(&date.succ_opt().unwrap().and_hms_opt(0, 0, 0).unwrap()).unwrap();
    (local_start.with_timezone(&Utc), local_end.with_timezone(&Utc))
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
