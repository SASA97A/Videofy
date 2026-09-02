# Spec: UTF-8 BOM Removal for FFmpeg Temp Files

## 1. Overview
Fixes the FFmpeg crash `-541478725` (`AVERROR_INVALIDDATA`) during video merging by removing the UTF-8 Byte Order Mark (BOM) from the generated temporary metadata (`ffmetadata.txt`) and concatenation list (`concat.txt`) files.

## 2. Requirements & Constraints
- Eliminate UTF-8 BOM from all generated files passed as inputs to FFmpeg commands.
- Use `new System.Text.UTF8Encoding(false)` in C# `StreamWriter` initializations to force BOM-less UTF-8 text writing.
- Verify build passes.
