using System.Collections.Generic;

namespace Jezda.Common.Abstractions.Configuration.Options;

/// <summary>
/// Hangfire configuration options, including authorization and job scheduling settings.
/// Provides settings for securing the Hangfire dashboard and defining scheduled jobs.
/// Gets populated from configuration sources.
/// </summary>
public class HangfireOptions
{
    /// <summary>
    /// Authorization settings for accessing the Hangfire dashboard.
    /// </summary>
    public Authorization Authorization { get; set; } = default!;

    /// <summary>
    /// Specifies the list of jobs to be scheduled and managed by Hangfire.
    /// </summary>
    public List<Job> Jobs { get; set; } = [];
}

public class Job
{
    /// <summary>
    /// Job identifier.
    /// </summary>
    public string Id { get; set; } = default!;

    /// <summary>
    /// Determines whether the job is enabled for scheduling.
    /// </summary>
    public bool IsEnabled { get; set; } = false;

    /// <summary>
    /// Defines the queue where the job will be executed.
    /// </summary>
    public string Queue { get; set; } = default!;

    /// <summary>
    /// Cron expression that defines the job's schedule.
    /// Possible values can be found at https://crontab.guru/.
    /// </summary>
    public string CronExpression { get; set; } = default!;
}

public class Authorization
{
    /// <summary>
    /// Username required for accessing the Hangfire dashboard.
    /// </summary>
    public string Username { get; set; } = default!;

    /// <summary>
    /// Password required for accessing the Hangfire dashboard.
    /// Keep this secure and do not expose it in public repositories.
    /// </summary>
    public string Password { get; set; } = default!;
}