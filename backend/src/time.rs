use chrono::{DateTime, NaiveDateTime, Utc};

const SQLITE_TIMESTAMP_FORMATS: &[&str] = &["%Y-%m-%d %H:%M:%S%.f", "%Y-%m-%d %H:%M:%S"];

pub fn timestamp_to_rfc3339(value: &str) -> String {
    if let Ok(parsed) = DateTime::parse_from_rfc3339(value) {
        return parsed.with_timezone(&Utc).to_rfc3339();
    }

    for format in SQLITE_TIMESTAMP_FORMATS {
        if let Ok(parsed) = NaiveDateTime::parse_from_str(value, format) {
            return DateTime::<Utc>::from_naive_utc_and_offset(parsed, Utc).to_rfc3339();
        }
    }

    value.to_string()
}
