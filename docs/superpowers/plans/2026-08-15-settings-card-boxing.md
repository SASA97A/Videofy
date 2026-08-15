# Settings Window Card Encapsulation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Encapsulate each category in Global Settings (`SettingsWindow.axaml`) inside visual card boxes (`Border` containers with `SecondaryBackground`, `BorderColor`, and `CornerRadius="6"`).

**Architecture:** Wrap the controls for Encoder Settings, Notification & Dialog Settings, and Misc & Advanced into `Border` card elements matching the Hardware Acceleration section. Update `releasenotes.md` and merge into `Version-1.4.3`.

**Tech Stack:** Avalonia XAML, C#.

## Global Constraints

- Card styling: `Background="{DynamicResource SecondaryBackground}"`, `BorderBrush="{DynamicResource BorderColor}"`, `BorderThickness="1"`, `CornerRadius="6"`, `Padding="14,12"`.
- Branch: `feature/settings-card-boxing` off `Version-1.4.3`.

---

### Task 1: Update SettingsWindow.axaml Layout

**Files:**
- Modify: `Video Size Optimizer/Views/SettingsWindow.axaml:16-196`

**Interfaces:**
- Consumes: XAML resources `SecondaryBackground`, `BorderColor`, `MainText`, `SystemAccentColor`.
- Produces: Encapsulated card sections in `SettingsWindow.axaml`.

- [ ] **Step 1: Update `SettingsWindow.axaml`**

Refactor `Video Size Optimizer/Views/SettingsWindow.axaml` content inside the main `ScrollViewer` StackPanel:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
		xmlns:vm="using:Video_Size_Optimizer.ViewModels"
        mc:Ignorable="d" d:DesignWidth="800" d:DesignHeight="450"
        x:Class="Video_Size_Optimizer.Views.SettingsWindow"
		x:DataType="vm:SettingsViewModel"
        Title="Global Settings" Width="440" Height="700"
        WindowStartupLocation="CenterOwner"
        Background="{DynamicResource MainBackground}" Foreground="{DynamicResource MainText}">

	<Grid RowDefinitions="*, Auto">
		<ScrollViewer Grid.Row="0" VerticalScrollBarVisibility="Auto">
			<StackPanel Margin="20" Spacing="20">
				<!-- Encoder Settings Category Box -->
				<StackPanel Spacing="8">
					<TextBlock Text="Encoder Settings" FontSize="16" FontWeight="Bold" Foreground="{DynamicResource SystemAccentColor}"/>

					<Border Background="{DynamicResource SecondaryBackground}"
							BorderBrush="{DynamicResource BorderColor}"
							BorderThickness="1"
							CornerRadius="6"
							Padding="14,12">
						<StackPanel Spacing="12">
							<StackPanel Spacing="5">
								<CheckBox IsChecked="{Binding DeleteOriginal}" Cursor="Hand" Content="Delete original file after successful compression" FontSize="13" Foreground="{DynamicResource MainText}" ToolTip.Tip="Automatically delete the source video file after successful compression (Warning: Cannot be undone!)"/>
								<StackPanel Orientation="Horizontal" Spacing="6" Margin="32,0,0,0">
									<Image Width="14" Height="14" VerticalAlignment="Center">
										<Image.Source>
											<SvgImage Source="/Assets/warning.svg"/>
										</Image.Source>
									</Image>
									<TextBlock Text="Warning: This action cannot be undone."
											   FontSize="12"
											   Foreground="{DynamicResource DangerColor}"
											   FontWeight="SemiBold"
											   VerticalAlignment="Center"/>
								</StackPanel>
							</StackPanel>

							<StackPanel Spacing="5">
								<CheckBox IsChecked="{Binding ProcessAlreadyOptimized}"
										  Cursor="Hand"
										  Content="Allow processing of already optimized videos"
										  FontSize="13"
										  Foreground="{DynamicResource MainText}"
										  ToolTip.Tip="Allow Videofy to re-process files containing output tags like '-CRF' or '-Target'"/>
								<TextBlock Text="Allows re-compressing files containing '-CRF' or '-Target'"
										   FontSize="12" Foreground="{DynamicResource SecondaryText}" Margin="32,0,0,0"/>
							</StackPanel>

							<StackPanel Spacing="5">
								<CheckBox IsChecked="{Binding PreventUpsampling}"
										  Cursor="Hand"
										  Content="Enable Bitrate Ceiling (Safety Cap)"
										  FontSize="13"
										  Foreground="{DynamicResource MainText}"
										  ToolTip.Tip="Caps output bitrate to prevent file size expansion on noisy or high-entropy videos"/>
								<TextBlock Text="Ensures output bitrate never exceeds original bitrate."
										   FontSize="12"
										   Foreground="{DynamicResource SecondaryText}"
										   Margin="32,0,0,0"
										   TextWrapping="Wrap"/>
							</StackPanel>

							<CheckBox IsChecked="{Binding PreventSleep}" Cursor="Hand" Content="Prevent system sleep during processing" FontSize="13" Foreground="{DynamicResource MainText}" ToolTip.Tip="Prevent computer from going to sleep while a batch encoding job is active"/>

							<StackPanel Spacing="5">
								<StackPanel Orientation="Horizontal" Spacing="10">
									<TextBlock Text="Pause if disk space is below:" FontSize="13" Foreground="{DynamicResource MainText}"/>
									<TextBlock Text="{Binding LowDiskBufferGb, StringFormat='{}{0} GB'}"
											   Foreground="{DynamicResource SystemAccentColor}" FontWeight="Bold" FontSize="13"/>
								</StackPanel>
								<Border Padding="10,0,10,0">
									<Slider Value="{Binding LowDiskBufferGb}" Minimum="2" Maximum="80"
											TickFrequency="1" IsSnapToTickEnabled="True" Cursor="Hand"/>
								</Border>
							</StackPanel>

							<StackPanel Spacing="6">
								<TextBlock Text="Default Output Format:" FontSize="13" Foreground="{DynamicResource MainText}"/>
								<ComboBox ItemsSource="{Binding OutputFormats}"
										  SelectedItem="{Binding SelectedFormat}"
										  Background="{DynamicResource InputBackground}"
										  BorderBrush="{DynamicResource BorderColor}"
										  Width="150" FontSize="13" Cursor="Hand"/>
							</StackPanel>
						</StackPanel>
					</Border>
				</StackPanel>

				<!-- Hardware Acceleration Category Box -->
				<StackPanel Spacing="8">
					<Grid ColumnDefinitions="*, Auto" Margin="2,0,0,0">
						<TextBlock Text="Hardware Acceleration"
								   FontSize="16"
								   FontWeight="Bold"
								   Foreground="{DynamicResource SystemAccentColor}"
								   VerticalAlignment="Center"/>
						<Button Grid.Column="1"
								Content="Auto-detect"
								Command="{Binding AutoDetectHardwareCommand}"
								Background="{DynamicResource SystemAccentColor}"
								Foreground="Black"
								FontWeight="SemiBold"
								FontSize="12"
								Padding="10,4"
								CornerRadius="4"
								Cursor="Hand"/>
					</Grid>

					<Border Background="{DynamicResource SecondaryBackground}"
							BorderBrush="{DynamicResource BorderColor}"
							BorderThickness="1"
							CornerRadius="6"
							Padding="14,12">

						<StackPanel Spacing="6">
							<TextBlock Text="Enable specific hardware encoders for your GPU:"
									   FontSize="12"
									   Foreground="{DynamicResource SecondaryText}"
									   Margin="0,0,0,6"/>

							<ItemsControl ItemsSource="{Binding EncoderOptions}">
								<ItemsControl.ItemTemplate>
									<DataTemplate>
										<CheckBox Content="{Binding Name}"
												  IsChecked="{Binding IsIncluded}"
												  IsEnabled="{Binding IsSupported}"
												  FontSize="12.5"
												  Margin="0,2"
												  Cursor="Hand"
												  Foreground="{DynamicResource MainText}"/>
									</DataTemplate>
								</ItemsControl.ItemTemplate>
							</ItemsControl>
						</StackPanel>
					</Border>
				</StackPanel>

				<!-- Notification & Dialog Settings Category Box -->
				<StackPanel Spacing="8">
					<TextBlock Text="Notification &amp; Dialog Settings" FontSize="16" FontWeight="Bold" Foreground="{DynamicResource SystemAccentColor}"/>

					<Border Background="{DynamicResource SecondaryBackground}"
							BorderBrush="{DynamicResource BorderColor}"
							BorderThickness="1"
							CornerRadius="6"
							Padding="14,12">
						<StackPanel Spacing="10">
							<CheckBox IsChecked="{Binding ModalCompletionMessages}"
									  Cursor="Hand"
									  Content="Block app input &amp; keep Task Completion messages on top"
									  FontSize="13"
									  Foreground="{DynamicResource MainText}"
									  ToolTip.Tip="When enabled, batch finished &amp; task completion popups stay on top and disable app window interaction until dismissed."/>

							<CheckBox IsChecked="{Binding ModalErrorMessages}"
									  Cursor="Hand"
									  Content="Block app input &amp; keep Error &amp; Warning messages on top"
									  FontSize="13"
									  Foreground="{DynamicResource MainText}"
									  ToolTip.Tip="When enabled, error and warning popups stay on top and disable app window interaction until dismissed."/>

							<CheckBox IsChecked="{Binding ModalInfoMessages}"
									  Cursor="Hand"
									  Content="Block app input &amp; keep Informational messages on top"
									  FontSize="13"
									  Foreground="{DynamicResource MainText}"
									  ToolTip.Tip="When enabled, informational and about popups stay on top and disable app window interaction until dismissed."/>
						</StackPanel>
					</Border>
				</StackPanel>

				<!-- Misc & Advanced Category Box -->
				<StackPanel Spacing="8">
					<TextBlock Text="Misc &amp; Advanced" FontSize="16" FontWeight="Bold" Foreground="{DynamicResource SystemAccentColor}"/>

					<Border Background="{DynamicResource SecondaryBackground}"
							BorderBrush="{DynamicResource BorderColor}"
							BorderThickness="1"
							CornerRadius="6"
							Padding="14,12">
						<StackPanel Spacing="12">
							<CheckBox IsChecked="{Binding AutoCheckUpdates}"
									  Cursor="Hand"
									  Content="Automatically check for updates on startup"
									  FontSize="13"
									  Foreground="{DynamicResource MainText}"/>

							<StackPanel Spacing="4">
								<TextBlock Text="Additional Input Formats:" FontSize="13" Foreground="{DynamicResource MainText}"/>
								<TextBox Text="{Binding CustomExtensions}"
										 Watermark="e.g. .mxf, .raw (comma separated)"
										 FontSize="12"
										 Background="{DynamicResource InputBackground}"
										 BorderBrush="{DynamicResource BorderColor}"
										 Foreground="{DynamicResource MainText}">
									<ToolTip.Tip>
										<StackPanel Spacing="2">
											<TextBlock Text="Videofy will attempt to read these formats when opening folders." FontWeight="SemiBold"/>
											<TextBlock Text="Note: Processing success depends on your local FFmpeg capabilities." Foreground="{DynamicResource WarningColor}" FontSize="12"/>
										</StackPanel>
									</ToolTip.Tip>
								</TextBox>
							</StackPanel>

							<StackPanel Spacing="4">
								<CheckBox IsChecked="{Binding UseSoftwareRendering}"
										  Cursor="Hand"
										  Content="Disable UI Hardware Acceleration"
										  FontSize="13"
										  Foreground="{DynamicResource MainText}"/>
								<TextBlock Text="Turn this on if you experience flickering, invisible tooltips, or UI lag. Requires settings to be saved then an application restart."
										   FontSize="12"
										   Foreground="{DynamicResource WarningColor}"
										   Margin="32,0,0,0"
										   TextWrapping="Wrap"/>
							</StackPanel>
						</StackPanel>
					</Border>
				</StackPanel>
			</StackPanel>
		</ScrollViewer>
		
		<Border Grid.Row="1" Background="{DynamicResource SecondaryBackground}" BorderBrush="{DynamicResource BorderColor}" BorderThickness="0,1,0,0" Padding="20,12">
			<Grid ColumnDefinitions="*, Auto">
				<CheckBox IsChecked="{Binding SaveToDisk}" Content="Remember settings" FontSize="13" Foreground="{DynamicResource MainText}" VerticalAlignment="Center" Cursor="Hand"/>
				<Button Grid.Column="1" Content="Close" Click="OnSaveClick" Background="{DynamicResource SystemAccentColor}" Foreground="Black" FontWeight="Bold" FontSize="13" Padding="18,8" CornerRadius="4" Cursor="Hand"/>
			</Grid>
		</Border>
	</Grid>
</Window>
```

- [ ] **Step 2: Build project and verify XAML syntax**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj"`
Expected: Build succeeded with 0 Errors.

- [ ] **Step 3: Commit**

```bash
git add "Video Size Optimizer/Views/SettingsWindow.axaml"
git commit -m "style: encapsulate settings categories into distinct visual card containers"
```

---

### Task 2: Update Release Notes

**Files:**
- Modify: `releasenotes.md:14-17`

**Interfaces:**
- Consumes: Release notes structure.
- Produces: Updated `releasenotes.md` under `## UI Enhancements`.

- [ ] **Step 1: Edit `releasenotes.md`**

Add the release entry under `## UI Enhancements`:

```markdown
- **Settings Card Layout Encapsulation:** Encapsulated all setting categories (Encoder Settings, Hardware Acceleration, Notification & Dialog Settings, Misc & Advanced) into distinct visual card containers (`Border` cards with rounded corners and subtle borders) for improved contrast, organization, and visual accessibility.
```

- [ ] **Step 2: Commit**

```bash
git add releasenotes.md
git commit -m "docs: add settings card encapsulation entry to release notes"
```

---

### Task 3: Final Verification & Merge

- [ ] **Step 1: Perform full Release build**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj" --configuration Release`
Expected: Build succeeded with 0 Errors.

- [ ] **Step 2: Checkout Version-1.4.3 and merge feature branch**

```bash
git checkout Version-1.4.3
git merge feature/settings-card-boxing
git branch -d feature/settings-card-boxing
```

- [ ] **Step 3: Verify clean build on Version-1.4.3**

Run: `dotnet build "Video Size Optimizer/Video Size Optimizer.csproj" --configuration Release`
Expected: Build succeeded with 0 Errors.
