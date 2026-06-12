using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lfm.Domain.ReadModels.ReviewModels.Subject;

namespace LFM.Domain.Read.Caching
{
    internal class SubjectCachingService
    {
        private readonly ConcurrentDictionary<int, (SubjectReviewModel Subject, DateTime ExpirationDateTime)> _subjectsCacheStorage;

        public SubjectCachingService()
        {
            _subjectsCacheStorage = new ConcurrentDictionary<int, (SubjectReviewModel, DateTime)>();
        }
        
        public Task<bool> TryGetById(int subjectId, out SubjectReviewModel subject)
        {
            subject = null;
            if (_subjectsCacheStorage.TryGetValue(subjectId, out var cachedValue))
            {
                if (DateTime.UtcNow >= cachedValue.ExpirationDateTime)
                {
                    _subjectsCacheStorage.Clear();
                }
                else
                {
                    subject = cachedValue.Subject;
                    return Task.FromResult(true);
                }
            }
            return Task.FromResult(false);
        }

        public Task<bool> TryGetAllSubjects(out ICollection<SubjectReviewModel> subjects)
        {
            subjects = null;
            if (_subjectsCacheStorage.Values.Count > 0)
            {
                if (DateTime.UtcNow >= _subjectsCacheStorage.Values.First().ExpirationDateTime)
                {
                    _subjectsCacheStorage.Clear();
                }
                else
                {
                    subjects = _subjectsCacheStorage.Values.Select(v => v.Subject).ToList();
                    return Task.FromResult(true);
                }
            }
            return Task.FromResult(false);
        }

        public Task<bool> TryCacheAllSubjects(ICollection<SubjectReviewModel> subjects)
        {
            _subjectsCacheStorage.Clear();
            
            var expirationDateTime = DateTime.UtcNow.AddHours(24);

            bool isAllAdded = true;
            foreach (var s in subjects)
            {
                if (!_subjectsCacheStorage.TryAdd(s.Id, (s, expirationDateTime)))
                {
                    isAllAdded = false;
                }
            }
            return Task.FromResult(isAllAdded);
        }
    }
}