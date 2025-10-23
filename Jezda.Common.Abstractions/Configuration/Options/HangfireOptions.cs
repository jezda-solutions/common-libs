using System.Collections.Generic;

namespace Jezda.Common.Abstractions.Configuration.Options;

public class HangfireOptions
{
    public Authorization Authorization { get; set; } = default!;

    public List<Job> Jobs { get; set; } = [];
}

public class Job
{
    public string Id { get; set; } = default!;

    public bool IsEnabled { get; set; } = false;

    public string Queue { get; set; } = default!;

    public string CronExpression { get; set; } = default!;
}

public class Authorization
{

    public string Username { get; set; } = default!;

    public string Password { get; set; } = default!;
}