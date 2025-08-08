# Large Performance Test File - 4000+ Words

This markdown file is designed to test large file loading performance and theme switching optimizations in ModernTextViewer. It contains approximately 4000+ words with various markdown elements to stress-test the rendering and theme switching capabilities.

## Introduction

Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum. Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium, totam rem aperiam, eaque ipsa quae ab illo inventore veritatis et quasi architecto beatae vitae dicta sunt explicabo.

### Complex List Structures

1. **First Level Item**: This is a complex nested list item that contains substantial text content to increase the overall document size and test rendering performance with large amounts of structured content.
   - Sub-item with **bold text** and *italic formatting*
   - Another sub-item with `inline code formatting` for technical content
   - Yet another sub-item with [hyperlink example](https://example.com) and more text content
   
2. **Second Level Item**: Another major list item with extensive content and multiple formatting elements to stress-test the markdown parser and HTML renderer.
   1. Numbered sub-item with detailed explanation
   2. Another numbered item with even more content
   3. Final numbered item in this section

3. **Third Level Item**: Final major item with complex content structure

## Code Examples Section

Here are various code examples in different languages to test syntax highlighting and large code block rendering:

### C# Example

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using ModernTextViewer.src.Models;

namespace ModernTextViewer.src.Services
{
    /// <summary>
    /// This is a comprehensive example of a C# service class that demonstrates
    /// various language features and would represent a typical business logic
    /// component in a modern .NET application architecture.
    /// </summary>
    public class ExampleService
    {
        private readonly ILogger<ExampleService> _logger;
        private readonly Dictionary<string, object> _cache;
        private const int MAX_RETRY_ATTEMPTS = 3;
        private const int BUFFER_SIZE = 4096;

        public ExampleService(ILogger<ExampleService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = new Dictionary<string, object>();
        }

        /// <summary>
        /// Asynchronously processes a collection of data items with retry logic
        /// and comprehensive error handling for production scenarios.
        /// </summary>
        public async Task<ProcessingResult> ProcessDataAsync(IEnumerable<DataItem> items)
        {
            var results = new List<ProcessedItem>();
            var errors = new List<ProcessingError>();

            foreach (var item in items)
            {
                try
                {
                    var processed = await ProcessSingleItemWithRetryAsync(item);
                    results.Add(processed);
                    _logger.LogInformation("Successfully processed item {ItemId}", item.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process item {ItemId}", item.Id);
                    errors.Add(new ProcessingError(item.Id, ex.Message));
                }
            }

            return new ProcessingResult(results, errors);
        }

        private async Task<ProcessedItem> ProcessSingleItemWithRetryAsync(DataItem item)
        {
            for (int attempt = 1; attempt <= MAX_RETRY_ATTEMPTS; attempt++)
            {
                try
                {
                    return await PerformComplexProcessingAsync(item);
                }
                catch (TemporaryException ex) when (attempt < MAX_RETRY_ATTEMPTS)
                {
                    _logger.LogWarning("Attempt {Attempt} failed for item {ItemId}: {Error}", 
                        attempt, item.Id, ex.Message);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                }
            }

            throw new ProcessingException($"Failed to process item {item.Id} after {MAX_RETRY_ATTEMPTS} attempts");
        }
    }
}
```

### JavaScript Example

```javascript
/**
 * Advanced JavaScript module demonstrating modern ES6+ features
 * and asynchronous programming patterns for web applications
 */

class DataProcessor {
    constructor(options = {}) {
        this.batchSize = options.batchSize || 100;
        this.maxConcurrency = options.maxConcurrency || 5;
        this.retryAttempts = options.retryAttempts || 3;
        this.cache = new Map();
        this.processingQueue = [];
        this.activeProcessing = new Set();
    }

    /**
     * Processes large datasets with batching and concurrency control
     * to optimize performance and prevent memory exhaustion
     */
    async processLargeDataset(dataset) {
        const batches = this.createBatches(dataset, this.batchSize);
        const results = [];
        
        // Process batches with concurrency limit
        const semaphore = new Semaphore(this.maxConcurrency);
        
        const batchPromises = batches.map(async (batch, index) => {
            await semaphore.acquire();
            
            try {
                console.log(`Processing batch ${index + 1}/${batches.length}`);
                const batchResult = await this.processBatch(batch);
                results.push(...batchResult);
            } catch (error) {
                console.error(`Batch ${index + 1} failed:`, error);
                throw error;
            } finally {
                semaphore.release();
            }
        });

        await Promise.all(batchPromises);
        return results;
    }

    async processBatch(batch) {
        const promises = batch.map(item => this.processItemWithRetry(item));
        return Promise.allSettled(promises).then(results => {
            return results
                .filter(result => result.status === 'fulfilled')
                .map(result => result.value);
        });
    }

    async processItemWithRetry(item, attempt = 1) {
        try {
            // Check cache first
            if (this.cache.has(item.id)) {
                return this.cache.get(item.id);
            }

            const result = await this.performComplexOperation(item);
            this.cache.set(item.id, result);
            return result;
        } catch (error) {
            if (attempt < this.retryAttempts) {
                console.warn(`Retrying item ${item.id}, attempt ${attempt + 1}`);
                await this.delay(Math.pow(2, attempt) * 1000);
                return this.processItemWithRetry(item, attempt + 1);
            }
            throw new ProcessingError(`Failed to process ${item.id}: ${error.message}`);
        }
    }

    createBatches(array, size) {
        const batches = [];
        for (let i = 0; i < array.length; i += size) {
            batches.push(array.slice(i, i + size));
        }
        return batches;
    }

    delay(ms) {
        return new Promise(resolve => setTimeout(resolve, ms));
    }
}

// Utility class for controlling concurrency
class Semaphore {
    constructor(maxCount) {
        this.maxCount = maxCount;
        this.currentCount = 0;
        this.queue = [];
    }

    async acquire() {
        return new Promise((resolve) => {
            if (this.currentCount < this.maxCount) {
                this.currentCount++;
                resolve();
            } else {
                this.queue.push(resolve);
            }
        });
    }

    release() {
        if (this.queue.length > 0) {
            const next = this.queue.shift();
            next();
        } else {
            this.currentCount--;
        }
    }
}

// Usage example
const processor = new DataProcessor({
    batchSize: 50,
    maxConcurrency: 3,
    retryAttempts: 5
});

// Export for use in other modules
export { DataProcessor, Semaphore };
```

### Python Example

```python
"""
Advanced Python module demonstrating modern Python features,
async programming, and data processing capabilities
"""

import asyncio
import aiohttp
import logging
from dataclasses import dataclass, field
from typing import List, Optional, Dict, Any, AsyncGenerator
from contextlib import asynccontextmanager
from datetime import datetime, timedelta
import json
import hashlib

@dataclass
class ProcessingConfig:
    """Configuration class for data processing operations"""
    batch_size: int = 100
    max_concurrency: int = 10
    retry_attempts: int = 3
    timeout_seconds: int = 30
    cache_ttl: int = 3600
    enable_logging: bool = True

@dataclass
class ProcessingResult:
    """Result container for processing operations"""
    success_count: int = 0
    error_count: int = 0
    results: List[Dict[str, Any]] = field(default_factory=list)
    errors: List[str] = field(default_factory=list)
    processing_time: float = 0.0

class AdvancedDataProcessor:
    """
    Advanced data processor with async capabilities, caching,
    and comprehensive error handling
    """
    
    def __init__(self, config: ProcessingConfig = None):
        self.config = config or ProcessingConfig()
        self.cache: Dict[str, tuple] = {}  # (data, timestamp)
        self.session: Optional[aiohttp.ClientSession] = None
        self.logger = self._setup_logging()
        
    def _setup_logging(self) -> logging.Logger:
        """Setup logging configuration"""
        logger = logging.getLogger(f"{__name__}.{self.__class__.__name__}")
        if self.config.enable_logging:
            handler = logging.StreamHandler()
            formatter = logging.Formatter(
                '%(asctime)s - %(name)s - %(levelname)s - %(message)s'
            )
            handler.setFormatter(formatter)
            logger.addHandler(handler)
            logger.setLevel(logging.INFO)
        return logger
    
    @asynccontextmanager
    async def get_session(self):
        """Async context manager for HTTP session handling"""
        if self.session is None:
            timeout = aiohttp.ClientTimeout(total=self.config.timeout_seconds)
            self.session = aiohttp.ClientSession(timeout=timeout)
        
        try:
            yield self.session
        finally:
            # Session cleanup handled in __aenter__/__aexit__
            pass
    
    async def process_large_dataset(
        self, 
        data_items: List[Dict[str, Any]]
    ) -> ProcessingResult:
        """
        Process a large dataset with batching, concurrency control,
        and comprehensive error handling
        """
        start_time = asyncio.get_event_loop().time()
        result = ProcessingResult()
        
        try:
            # Create batches for processing
            batches = [
                data_items[i:i + self.config.batch_size]
                for i in range(0, len(data_items), self.config.batch_size)
            ]
            
            self.logger.info(f"Processing {len(data_items)} items in {len(batches)} batches")
            
            # Create semaphore for concurrency control
            semaphore = asyncio.Semaphore(self.config.max_concurrency)
            
            # Process all batches concurrently
            tasks = [
                self._process_batch_with_semaphore(batch, idx, semaphore)
                for idx, batch in enumerate(batches)
            ]
            
            batch_results = await asyncio.gather(*tasks, return_exceptions=True)
            
            # Aggregate results
            for batch_result in batch_results:
                if isinstance(batch_result, Exception):
                    result.error_count += 1
                    result.errors.append(str(batch_result))
                else:
                    result.success_count += batch_result.success_count
                    result.error_count += batch_result.error_count
                    result.results.extend(batch_result.results)
                    result.errors.extend(batch_result.errors)
            
        except Exception as e:
            self.logger.error(f"Critical error in dataset processing: {e}")
            result.errors.append(f"Critical processing error: {e}")
        
        finally:
            result.processing_time = asyncio.get_event_loop().time() - start_time
            if self.session:
                await self.session.close()
                self.session = None
        
        return result
    
    async def _process_batch_with_semaphore(
        self, 
        batch: List[Dict[str, Any]], 
        batch_idx: int, 
        semaphore: asyncio.Semaphore
    ) -> ProcessingResult:
        """Process a single batch with semaphore-controlled concurrency"""
        async with semaphore:
            return await self._process_batch(batch, batch_idx)
    
    async def _process_batch(
        self, 
        batch: List[Dict[str, Any]], 
        batch_idx: int
    ) -> ProcessingResult:
        """Process a single batch of data items"""
        batch_result = ProcessingResult()
        
        self.logger.info(f"Starting batch {batch_idx + 1} with {len(batch)} items")
        
        # Process items in batch concurrently
        tasks = [self._process_single_item(item) for item in batch]
        item_results = await asyncio.gather(*tasks, return_exceptions=True)
        
        # Aggregate batch results
        for item_result in item_results:
            if isinstance(item_result, Exception):
                batch_result.error_count += 1
                batch_result.errors.append(str(item_result))
            else:
                batch_result.success_count += 1
                batch_result.results.append(item_result)
        
        self.logger.info(
            f"Batch {batch_idx + 1} completed: "
            f"{batch_result.success_count} success, {batch_result.error_count} errors"
        )
        
        return batch_result
    
    async def _process_single_item(self, item: Dict[str, Any]) -> Dict[str, Any]:
        """Process a single data item with retry logic"""
        item_id = item.get('id', 'unknown')
        
        # Check cache first
        cache_key = self._generate_cache_key(item)
        cached_result = self._get_from_cache(cache_key)
        if cached_result:
            return cached_result
        
        # Process with retry logic
        last_exception = None
        for attempt in range(1, self.config.retry_attempts + 1):
            try:
                result = await self._perform_complex_operation(item)
                
                # Cache successful result
                self._store_in_cache(cache_key, result)
                return result
                
            except Exception as e:
                last_exception = e
                if attempt < self.config.retry_attempts:
                    delay = 2 ** (attempt - 1)  # Exponential backoff
                    self.logger.warning(
                        f"Attempt {attempt} failed for item {item_id}, "
                        f"retrying in {delay}s: {e}"
                    )
                    await asyncio.sleep(delay)
                else:
                    self.logger.error(
                        f"All {self.config.retry_attempts} attempts failed for item {item_id}: {e}"
                    )
        
        raise last_exception
    
    def _generate_cache_key(self, item: Dict[str, Any]) -> str:
        """Generate a cache key for the given item"""
        item_str = json.dumps(item, sort_keys=True)
        return hashlib.md5(item_str.encode()).hexdigest()
    
    def _get_from_cache(self, cache_key: str) -> Optional[Dict[str, Any]]:
        """Retrieve item from cache if not expired"""
        if cache_key in self.cache:
            data, timestamp = self.cache[cache_key]
            if datetime.now() - timestamp < timedelta(seconds=self.config.cache_ttl):
                return data
            else:
                del self.cache[cache_key]
        return None
    
    def _store_in_cache(self, cache_key: str, data: Dict[str, Any]) -> None:
        """Store item in cache with timestamp"""
        self.cache[cache_key] = (data, datetime.now())
    
    async def _perform_complex_operation(self, item: Dict[str, Any]) -> Dict[str, Any]:
        """Simulate complex operation (replace with actual business logic)"""
        async with self.get_session() as session:
            # Simulate processing time
            await asyncio.sleep(0.1)
            
            # Simulate complex transformation
            result = {
                'id': item.get('id'),
                'processed_at': datetime.now().isoformat(),
                'result': f"processed_{item.get('value', 'unknown')}",
                'metadata': {
                    'processing_version': '1.0',
                    'complexity_score': len(str(item)) * 1.5,
                    'transformations_applied': ['normalize', 'validate', 'enrich']
                }
            }
            
            return result

# Usage example
async def main():
    """Example usage of the AdvancedDataProcessor"""
    config = ProcessingConfig(
        batch_size=25,
        max_concurrency=5,
        retry_attempts=3,
        timeout_seconds=60
    )
    
    processor = AdvancedDataProcessor(config)
    
    # Generate sample data
    sample_data = [
        {'id': f'item_{i}', 'value': f'data_{i}', 'complexity': i % 10}
        for i in range(500)
    ]
    
    # Process the dataset
    result = await processor.process_large_dataset(sample_data)
    
    print(f"Processing completed in {result.processing_time:.2f} seconds")
    print(f"Success: {result.success_count}, Errors: {result.error_count}")

if __name__ == "__main__":
    asyncio.run(main())
```

## Tables and Complex Formatting

| Column 1 Header | Column 2 Header | Column 3 Header | Column 4 Header |
|----------------|-----------------|-----------------|-----------------|
| Long content in first cell with detailed information that spans multiple lines when rendered | **Bold content** with formatting | `Code content` with technical details | *Italic content* with emphasis |
| Another row with substantial content for testing table rendering performance | Complex data with various formats | More code examples `const x = 42;` | Additional formatted text |
| Third row containing even more content to increase document complexity | Tables are important for data presentation | JavaScript: `function test() { return true; }` | Final column content |
| Fourth row with comprehensive content for thorough testing purposes | Performance testing requires substantial content | C#: `public class Test { }` | End of table content |

## Blockquotes and Complex Formatting

> This is a complex blockquote that contains substantial text content designed to test the rendering performance of the markdown processor and HTML generator in the ModernTextViewer application. 
> 
> > This is a nested blockquote within the main blockquote, which adds another layer of complexity to the document structure and tests the ability to handle nested formatting elements effectively.
> > 
> > ### Header within nested blockquote
> > 
> > Even more content within the nested structure to increase complexity.
> 
> Back to the main blockquote level with additional content and formatting elements.

### Task Lists

- [x] Completed task with substantial description text that provides comprehensive details about the task implementation and requirements for proper testing
- [x] Another completed task with detailed information about performance optimization techniques and implementation strategies
- [ ] Pending task with extensive description covering multiple aspects of development work and quality assurance processes
- [ ] Another pending task covering advanced topics in software architecture and design patterns for modern applications
- [x] Final completed task with comprehensive coverage of testing methodologies and performance measurement techniques

## Performance Testing Sections

### Section A - Substantial Content Block

This section contains substantial text content designed specifically for performance testing of the ModernTextViewer application. The content includes various formatting elements, technical information, and comprehensive descriptions that simulate real-world document usage patterns. Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.

Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium, totam rem aperiam, eaque ipsa quae ab illo inventore veritatis et quasi architecto beatae vitae dicta sunt explicabo. Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit, sed quia consequuntur magni dolores eos qui ratione voluptatem sequi nesciunt.

### Section B - Technical Documentation

The following section provides detailed technical documentation about software architecture patterns, implementation strategies, and best practices for modern application development. This content is designed to simulate real-world technical documentation that would be commonly viewed and edited in a text editor application.

#### Subsection B.1 - Architecture Patterns

Model-View-Controller (MVC) architecture is a software design pattern that separates an application into three interconnected components. This separation of concerns allows for efficient code organization and maintainability. The Model represents the data and business logic, the View handles the user interface presentation, and the Controller manages user input and coordinates between the Model and View.

#### Subsection B.2 - Implementation Strategies

When implementing large-scale applications, several key strategies should be considered for optimal performance and maintainability. These include dependency injection for loose coupling, asynchronous programming for responsiveness, caching strategies for performance optimization, and comprehensive error handling for reliability.

### Section C - Additional Performance Content

This final section provides additional substantial content to ensure the document meets the 4000+ word requirement for comprehensive performance testing. The content includes detailed explanations of various technical concepts, implementation examples, and best practices for software development.

The importance of performance optimization in modern applications cannot be overstated. Users expect responsive interfaces and quick load times, regardless of the size or complexity of the content being processed. This is particularly true for text editors and document viewers, where users may work with large files containing thousands of lines of code, documentation, or other textual content.

Performance optimization techniques include efficient memory management, optimized algorithms for text processing, asynchronous operations for file I/O, and intelligent caching mechanisms. Additionally, user interface responsiveness is crucial, requiring careful consideration of thread management and progressive loading strategies.

## Conclusion

This comprehensive test document contains substantial content designed to thoroughly test the performance optimizations implemented in ModernTextViewer. The document includes various markdown elements such as headers, lists, code blocks, tables, blockquotes, task lists, and extensive text content to simulate real-world usage scenarios and stress-test the application's performance capabilities.

The optimizations being tested include async file loading, theme switching performance, WebView2 rendering efficiency, and overall application responsiveness when handling large content files. These improvements are critical for providing a smooth user experience when working with substantial documents.

Total word count: Approximately 4000+ words with comprehensive formatting elements for thorough performance testing.