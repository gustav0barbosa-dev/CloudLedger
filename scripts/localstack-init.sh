#!/bin/sh
# Provisiona os recursos AWS usados localmente pelo CloudLedger (LocalStack).
set -e

awslocal sqs create-queue --queue-name cloudledger-domain-events --attributes VisibilityTimeout=60,MessageRetentionPeriod=1209600 || true

awslocal dynamodb create-table \
  --table-name cloudledger-idempotency \
  --attribute-definitions AttributeName=PK,AttributeType=S \
  --key-schema AttributeName=PK,KeyType=HASH \
  --billing-mode PAY_PER_REQUEST || true

awslocal dynamodb update-time-to-live \
  --table-name cloudledger-idempotency \
  --time-to-live-specification Enabled=true,AttributeName=ExpiresAt || true

awslocal s3 mb s3://cloudledger-reports || true
awslocal s3api put-public-access-block \
  --bucket cloudledger-reports \
  --public-access-block-configuration BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true || true

echo "CloudLedger: recursos LocalsStack provisionados."
