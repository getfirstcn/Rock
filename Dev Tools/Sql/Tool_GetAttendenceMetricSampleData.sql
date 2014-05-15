/* Creates INSERT Sql from data from another database for metrics */
SELECT concat(
  'INSERT INTO [MetricValue] ([MetricId], [MetricValueType], [XValue], [YValue], [Order], [Note], [MetricValueDateTime], [Guid], [EntityId])
    VALUES (@metricId, 0, ''', metric_x_value, ''',',  metric_value, ',', '0', ',''', replace(note, '''', ''''''), ''',''', collection_date, ''',''', NEWID(), ''',', metric_id, ')')
  FROM [Arena].[dbo].[mtrc_metric_item]
where metric_id in (158,249,286,313,325)


