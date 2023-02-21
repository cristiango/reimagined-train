Continuous delivery is a software development practice where code changes are automatically prepared for a release to production. A pillar of modern application development, continuous delivery expands upon continuous integration by deploying all code changes to a testing environment and/or a production environment after the build stage. When properly implemented, developers will always have a deployment-ready build artifact that has passed through a standardized test process. 

The practices and patterns with this approach is the most significant difference in how we work today when comparing to on-premise and managed service delivery models. It is a set of practices that are a hall mark of SaaS based organisations. As we won’t transition from one model to another over night, both approaches would be applied in parallel. However, operating with two models will be a mindset challenge for many.

The specific practices & principals adopted were:

You build it, you run it.
The teams owns the code, the pipeline, the infra and have responsibility for operating the services they develop. https://www.atlassian.com/incident-management/devops/you-built-it-you-run-it

Fast builds & fast deploys.
We want the absolute max time of 15 min from a code change to production. This doesn’t mean we're actually updating production straight away after a commit. However when dealing with an outage/incident, every minute will count. This is will be a driver for deploying smaller “parts” instead of whole services/systems/multiple apps at a time.

Trunk based development.
What is on the main branch is what is deployed to production. https://trunkbaseddevelopment.com/

Branch by abstraction.
An approach to making “longer to complete” changes while practicing Trunk Based Development. Branch by abstraction.

No breaking changes, ever.​
Main is always production deployable. Changes that might be considered breaking with on-premise style delivery are performed in a different way - instead multiple small non-breaking changes are performed and deployed eventually arriving at the state a breaking change would have made.

One version in production.
Versioning, such as with APIs, features and Domains are modelled in the code base side by side with currently supported versions​.

Feature Toggles.
Releasing a feature is entirely decoupled from deployment. Instead we use feature toggles to “turn on” capability on a per-tenant basis. This will allow new concepts to be brought to market / customers, verified, tested etc all in production. https://www.wix.engineering/post/continuous-delivery-feature-toggles

Testing/UAT/QA in production.
As a side effect of Trunk Based Development and Feature toggles, functional UAT and QA are performed in production systems. Isolation will be at tenant level through feature toggles and at system level through point of deployment. https://www.browserstack.com/guide/testing-in-production

Everything is automated. No exceptions (except incidents).

Continuous Delivery/Deployment and On-Premise and Managed Services
It is not possible to practice CD with an on-premise model unless one is also operating the on-premise deployments. Current each customer has different operating models (e.g. outsourced ops teams) and different technology & tooling approaches where the lowest common denominator wins out. Many also have a manual OAT (Operational Acceptance Testing) procedure. Essentially none of the above practices and principals can be applied.

With a Managed Services model there is, minimally, a “hand over” between departmental boundaries - a transfer of ownership and responsibilities. UAT Testing is manual usually requires customer involvement, operational testing in required and product management & developers are disconnected / abstracted from the running product. The pipeline can be optimized because at least there would be an agreed target operating model, tool chain etc. However, most of the above practices and principals can’t be applied.
