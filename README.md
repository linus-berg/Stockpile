# Stockpile

Stockpile is used to collect (stockpile) large amounts of packages from multiple package managers.
Primary purpose is to supply air gapped registries with artifacts.

## Usage/Examples
#### Specifying configuration path.
```bash
stockpile --config /some/path/config.json ...
```
#### Only populate metadata stores and stockpile the main deposit.
```bash
stockpile --staging 
```
#### Blacklist a package version from being stockpiled. (upcoming feature)
```bash
stockpile blacklist --channelId npm --artifactId react --version 16.0.0
```


| Channel       | Supported     |
|:-------------:|:-------------:|
| Nuget    | :heavy_check_mark: |
| Npm      | :heavy_check_mark: |
| Git      | :heavy_check_mark: |
| Docker      | :heavy_check_mark: |
| Helm Charts     | :heavy_check_mark: |
| Maven    | :heavy_plus_sign:  |
