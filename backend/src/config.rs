use std::fmt;
use std::fs;
use std::path::{Path, PathBuf};

use anyhow::{anyhow, Context, Result};
use serde::{Deserialize, Serialize};

use crate::domain::{AdminMode, PlatformId, Visibility};

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub struct AppConfig {
    pub server: ServerConfig,
    #[serde(default)]
    pub frontend: FrontendConfig,
    pub storage: StorageConfig,
    pub auth: AuthConfig,
    #[serde(default)]
    pub integrations: IntegrationsConfig,
    #[serde(default)]
    pub libraries: Vec<LibraryConfig>,
    #[serde(default)]
    pub platforms: Vec<PlatformConfig>,
}

impl AppConfig {
    pub fn load_from_file(path: impl AsRef<Path>) -> Result<Self> {
        let path = path.as_ref();
        let raw = fs::read_to_string(path)
            .with_context(|| format!("failed to read config file {}", path.display()))?;
        let config: Self = toml::from_str(&raw)
            .with_context(|| format!("failed to parse TOML config at {}", path.display()))?;
        config.validate()?;
        Ok(config)
    }

    pub fn validate(&self) -> Result<()> {
        let mut errors = Vec::new();

        if self.server.listen_address.trim().is_empty() {
            errors.push("server.listen_address must not be empty".to_string());
        }
        if self.server.port == 0 {
            errors.push("server.port must be greater than 0".to_string());
        }
        if matches!(
            self.frontend.dev_listen_address.as_deref(),
            Some(value) if value.trim().is_empty()
        ) {
            errors.push("frontend.dev_listen_address must not be empty".to_string());
        }
        if self.frontend.dev_port == 0 {
            errors.push("frontend.dev_port must be greater than 0".to_string());
        }

        validate_path(
            "storage.database_path",
            &self.storage.database_path,
            true,
            &mut errors,
        );
        validate_path("storage.cache_dir", &self.storage.cache_dir, false, &mut errors);
        if let Some(temp_dir) = &self.storage.temp_dir {
            validate_path("storage.temp_dir", temp_dir, false, &mut errors);
        }
        if self.storage.split_part_size_bytes == 0 {
            errors.push("storage.split_part_size_bytes must be greater than 0".to_string());
        }

        match self.auth.admin_mode {
            AdminMode::Local => {
                require_path(
                    "auth.owner_password_hash_file",
                    self.auth.owner_password_hash_file.as_ref(),
                    &mut errors,
                );
            }
            AdminMode::Proxy => {
                require_non_empty(
                    "auth.proxy_user_header",
                    self.auth.proxy_user_header.as_deref(),
                    &mut errors,
                );
            }
        }

        if self.integrations.playnite.enabled {
            if let Some(default_platform) = &self.integrations.playnite.default_platform {
                if !self.platforms.iter().any(|platform| &platform.id == default_platform) {
                    errors.push(format!(
                        "integrations.playnite.default_platform references unknown platform '{}'",
                        default_platform
                    ));
                }
            }
        }

        if self.libraries.is_empty() {
            errors.push("at least one library must be configured".to_string());
        }

        if self.platforms.is_empty() {
            errors.push("at least one platform must be configured".to_string());
        }

        let mut seen_library_names = std::collections::BTreeSet::new();
        for library in &self.libraries {
            if !seen_library_names.insert(library.name.clone()) {
                errors.push(format!("duplicate library name '{}'", library.name));
            }
            validate_path(
                &format!("libraries[{}].root_path", library.name),
                &library.root_path,
                false,
                &mut errors,
            );
            if !self.platforms.iter().any(|platform| platform.id == library.platform) {
                errors.push(format!(
                    "library '{}' references unknown platform '{}'",
                    library.name, library.platform
                ));
            }
        }

        let mut seen_platforms = std::collections::BTreeSet::new();
        for platform in &self.platforms {
            if platform.id.0.trim().is_empty() {
                errors.push("platforms[].id must not be empty".to_string());
            }
            if platform.match_priority < 0 {
                errors.push(format!(
                    "platform '{}' has invalid negative match_priority {}",
                    platform.id, platform.match_priority
                ));
            }
            if !seen_platforms.insert(platform.id.clone()) {
                errors.push(format!("duplicate platform id '{}'", platform.id));
            }
        }

        if errors.is_empty() {
            Ok(())
        } else {
            Err(anyhow!(ConfigValidationError { errors }))
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub struct ServerConfig {
    pub listen_address: String,
    pub port: u16,
    #[serde(default)]
    pub trusted_proxies: Vec<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub struct FrontendConfig {
    #[serde(default)]
    pub dev_listen_address: Option<String>,
    #[serde(default = "default_frontend_dev_port")]
    pub dev_port: u16,
}

impl Default for FrontendConfig {
    fn default() -> Self {
        Self {
            dev_listen_address: None,
            dev_port: default_frontend_dev_port(),
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub struct StorageConfig {
    pub database_path: PathBuf,
    pub cache_dir: PathBuf,
    #[serde(default)]
    pub temp_dir: Option<PathBuf>,
    #[serde(default = "default_split_part_size_bytes")]
    pub split_part_size_bytes: u64,
    #[serde(default = "default_deduplicate_by_checksum")]
    pub deduplicate_by_checksum: bool,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub struct AuthConfig {
    pub admin_mode: AdminMode,
    #[serde(default)]
    pub owner_password_hash_file: Option<PathBuf>,
    #[serde(default)]
    pub proxy_user_header: Option<String>,
    #[serde(default)]
    pub proxy_email_header: Option<String>,
    #[serde(default)]
    pub trusted_proxy_headers: Vec<String>,
}

#[derive(Debug, Clone, Default, Serialize, Deserialize, PartialEq, Eq)]
pub struct IntegrationsConfig {
    #[serde(default)]
    pub playnite: PlayniteIntegrationConfig,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub struct PlayniteIntegrationConfig {
    #[serde(default)]
    pub enabled: bool,
    #[serde(default)]
    pub allow_uploads: bool,
    #[serde(default)]
    pub default_platform: Option<PlatformId>,
    #[serde(default)]
    pub token_label_prefix: Option<String>,
}

impl Default for PlayniteIntegrationConfig {
    fn default() -> Self {
        Self {
            enabled: false,
            allow_uploads: false,
            default_platform: None,
            token_label_prefix: None,
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub struct LibraryConfig {
    pub name: String,
    pub root_path: PathBuf,
    pub platform: PlatformId,
    pub visibility: Visibility,
    pub enabled: bool,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub struct PlatformConfig {
    pub id: PlatformId,
    pub enabled: bool,
    pub match_priority: i32,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ConfigValidationError {
    pub errors: Vec<String>,
}

impl fmt::Display for ConfigValidationError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        writeln!(f, "config validation failed:")?;
        for error in &self.errors {
            writeln!(f, "- {error}")?;
        }
        Ok(())
    }
}

impl std::error::Error for ConfigValidationError {}

fn validate_path(name: &str, path: &Path, allow_file: bool, errors: &mut Vec<String>) {
    let raw = path.as_os_str().to_string_lossy();
    if raw.trim().is_empty() {
        errors.push(format!("{name} must not be empty"));
        return;
    }

    if !allow_file && raw.ends_with(std::path::MAIN_SEPARATOR) {
        errors.push(format!("{name} must not end with a path separator"));
    }
}

fn require_path(name: &str, path: Option<&PathBuf>, errors: &mut Vec<String>) {
    match path {
        Some(path) => validate_path(name, path, true, errors),
        None => errors.push(format!("{name} is required")),
    }
}

fn require_non_empty(name: &str, value: Option<&str>, errors: &mut Vec<String>) {
    match value {
        Some(value) if !value.trim().is_empty() => {}
        _ => errors.push(format!("{name} is required")),
    }
}

fn default_split_part_size_bytes() -> u64 {
    2 * 1024 * 1024 * 1024
}

fn default_frontend_dev_port() -> u16 {
    4173
}

fn default_deduplicate_by_checksum() -> bool {
    true
}

#[cfg(test)]
mod tests {
    use rust_embed::RustEmbed;

    use super::AppConfig;

    #[derive(RustEmbed)]
    #[folder = "../config/"]
    struct ConfigAssets;

    #[test]
    fn validates_example_config() {
        let raw = ConfigAssets::get("gumo.example.toml")
            .expect("embedded example config should exist");
        let raw = std::str::from_utf8(raw.data.as_ref())
            .expect("embedded example config should be valid UTF-8");
        let config: AppConfig = toml::from_str(raw).expect("example config should parse");
        config.validate().expect("example config should validate");
    }

    #[test]
    fn rejects_library_referencing_unknown_platform() {
        let raw = r#"
[server]
listen_address = "127.0.0.1"
port = 8080

[storage]
database_path = "./gumo.db"
cache_dir = "./cache"

[auth]
admin_mode = "local"
owner_password_hash_file = "./password"

[integrations.playnite]
enabled = true
allow_uploads = true
default_platform = "pc"

[[platforms]]
id = "pc"
enabled = true
match_priority = 100

[[libraries]]
name = "primary"
root_path = "./storage"
platform = "switch"
visibility = "private"
enabled = true
"#;

        let config: AppConfig = toml::from_str(raw).expect("config should parse");
        let error = config.validate().expect_err("config should fail validation");
        let message = error.to_string();
        assert!(message.contains("unknown platform 'switch'"));
    }

    #[test]
    fn rejects_proxy_auth_without_header() {
        let raw = r#"
[server]
listen_address = "127.0.0.1"
port = 8080

[storage]
database_path = "./gumo.db"
cache_dir = "./cache"

[auth]
admin_mode = "proxy"

[[platforms]]
id = "pc"
enabled = true
match_priority = 100

[[libraries]]
name = "primary"
root_path = "./storage"
platform = "pc"
visibility = "private"
enabled = true
"#;

        let config: AppConfig = toml::from_str(raw).expect("config should parse");
        let error = config.validate().expect_err("config should fail validation");
        assert!(error.to_string().contains("auth.proxy_user_header is required"));
    }
}
