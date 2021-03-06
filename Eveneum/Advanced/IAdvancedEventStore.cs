﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Eveneum.Advanced
{
    public interface IAdvancedEventStore
    {
        Task LoadAllEvents(Func<IReadOnlyCollection<EventData>, Task> callback, CancellationToken cancellationToken = default);
    }
}
